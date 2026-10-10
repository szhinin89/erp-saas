using System.Text.Json;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Common;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Payables.Entities;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// IL-6B — confirmación atómica de la CxP Inicial. Primero se revalida TODO el lote contra el estado
/// actual (stale preview); solo si ninguna fila cambió se crea una <c>AccountsPayable</c> InitialBalance
/// por fila (<c>OriginId</c> = fila del lote, una cuota por el saldo neto con su vencimiento,
/// <c>AccountingDate</c> = <c>Company.OpeningBalanceDate</c>). Cualquier fallo devuelve
/// <see cref="BatchConfirmResult.Failed"/> y el handler revierte la transacción completa. Nunca crea
/// compras, gastos, proveedores, retenciones ni asientos (contabilidad de la apertura → IL-7).
/// </summary>
public sealed partial class InitialPayableImportProcessor
{
    public Task<BatchConfirmResult> ConfirmBatchAsync(
        IReadOnlyList<(int RowNumber, string ParsedDataJson)> rows, CancellationToken ct) =>
        Task.FromResult(BatchConfirmResult.Failed("La CxP inicial requiere el lote y sus filas de origen para confirmarse."));

    public Task<BatchConfirmResult> ConfirmBatchAsync(
        Guid importBatchId, IReadOnlyList<(int RowNumber, string ParsedDataJson)> rows, CancellationToken ct) =>
        Task.FromResult(BatchConfirmResult.Failed("La CxP inicial requiere las filas de origen para confirmarse."));

    public async Task<BatchConfirmResult> ConfirmBatchAsync(
        Guid importBatchId, IReadOnlyList<(Guid RowId, int RowNumber, string ParsedDataJson)> rows, CancellationToken ct)
    {
        var parsed = rows
            .Select(r => (r.RowId, r.RowNumber, Row: JsonSerializer.Deserialize<ParsedInitialPayableRow>(r.ParsedDataJson)!))
            .ToList();
        if (parsed.Count == 0)
            return BatchConfirmResult.Failed("El lote no tiene filas para confirmar.");

        var (batchError, openingBalanceDate) = await RevalidateBatchAsync(parsed.Select(p => p.Row).ToList(), ct);
        if (batchError is not null)
            return BatchConfirmResult.Failed($"{batchError} Vuelva a validar el archivo.");

        var activeDocTypes = (await _sriCatalog.GetActiveDocTypesAsync(ct))
            .Select(d => d.Code).ToHashSet(StringComparer.Ordinal);
        var existingKeys = new Dictionary<Guid, HashSet<string>>();
        foreach (var (_, rowNumber, row) in parsed)
        {
            var error = await RevalidateRowAsync(row, activeDocTypes, existingKeys, ct);
            if (error is not null)
                return BatchConfirmResult.Failed($"Fila {rowNumber}: {error} Vuelva a validar el archivo.");
        }

        var created = new Dictionary<int, Guid>();
        foreach (var (rowId, rowNumber, row) in parsed)
        {
            var payable = AccountsPayable.CreateInitialBalance(
                _ctx.TenantId,
                _ctx.CompanyId,
                row.BranchId,
                row.SupplierId,
                row.DocumentType,
                row.DocumentNumber,
                row.IssueDate!.Value,
                row.DueDate!.Value,
                openingBalanceDate,
                row.Balance,
                importBatchId,
                rowId,
                _ctx.UserId
            );
            await _payables.AddAsync(payable, ct);
            created[rowNumber] = payable.Id;
        }
        await _payables.SaveChangesAsync(ct);
        return BatchConfirmResult.Success(created);
    }

    /// <summary>
    /// Reglas de lote: sucursal activa, un único corte igual a <c>Company.OpeningBalanceDate</c>
    /// vigente (no futuro) y ningún documento repetido dentro del lote.
    /// </summary>
    private async Task<(string? Error, DateOnly OpeningBalanceDate)> RevalidateBatchAsync(
        IReadOnlyList<ParsedInitialPayableRow> rows, CancellationToken ct)
    {
        if (_branch.BranchId == Guid.Empty || rows.Any(r => r.BranchId != _branch.BranchId))
            return ("El lote pertenece a otra sucursal o no hay sucursal activa.", default);

        var cutoffs = rows.Select(r => r.CutoffDate).Distinct().ToList();
        if (cutoffs.Count != 1 || cutoffs[0] is not { } cutoff)
            return ("El lote debe tener una única fecha de corte.", default);
        var opening = await _openingBalance.GetOpeningBalanceDateAsync(ct);
        if (OpeningBalanceImportRules.CutoffNotOpeningDateOnConfirm(cutoff, opening) is { } cutoffError)
            return (cutoffError, default);
        var today = await _clock.TodayAsync(_ctx.CompanyId, _ctx.TenantId, ct);
        if (cutoff > today)
            return ($"La fecha de corte {cutoff:yyyy-MM-dd} es posterior a hoy.", default);

        if (rows.GroupBy(r => (r.SupplierId, r.DocumentKey)).Any(g => g.Count() > 1))
            return ("El lote repite un documento del mismo proveedor.", default);
        return (null, cutoff);
    }

    /// <summary>Estado actual del proveedor, tipo y unicidad del documento, y forma de la fila.</summary>
    private async Task<string?> RevalidateRowAsync(
        ParsedInitialPayableRow row, HashSet<string> activeDocTypes,
        Dictionary<Guid, HashSet<string>> existingKeys, CancellationToken ct)
    {
        if (row.SupplierId == Guid.Empty || row.IssueDate is not { } issue || row.DueDate is not { } due
            || row.CutoffDate is not { } cutoff)
            return "la fila no está validada.";
        if (row.CurrencyCode != OpeningBalanceImportRules.SupportedCurrency)
            return $"la moneda '{row.CurrencyCode}' no está admitida.";
        if (!OpeningBalanceImportRules.IsValidBalance(row.Balance))
            return "el saldo pendiente no es válido.";
        if (issue > cutoff || due < issue)
            return "las fechas de emisión/vencimiento no son coherentes con el corte.";
        if (row.DocumentKey.Length == 0 || row.DocumentNumber.Length > AccountsPayable.DocumentNumberMaxLen
            || DocumentNumberKey.Normalize(row.DocumentNumber) != row.DocumentKey)
            return "el número de documento no es válido.";
        if (!activeDocTypes.Contains(row.DocumentType))
            return $"el tipo de documento '{row.DocumentType}' ya no está activo en el catálogo SRI.";
        if (NonPayableDocTypes.Contains(row.DocumentType))
            return $"el tipo de documento '{row.DocumentType}' no representa una deuda con el proveedor.";

        var match = await _partnerLookup.FindByIdentificationAsync(
            row.IdentificationType, row.IdentificationNumber, RoleType.Supplier, ct);
        if (match is null || match.BusinessPartnerId != row.SupplierId || match.IsAmbiguous)
            return $"el proveedor {row.IdentificationNumber} ya no existe en el maestro.";
        if (!match.IsActive)
            return $"el proveedor {match.LegalName} fue inactivado.";
        if (!match.HasActiveRole)
            return $"el proveedor {match.LegalName} ya no tiene rol Proveedor activo.";

        if (!existingKeys.TryGetValue(row.SupplierId, out var keys))
        {
            keys = (await _payableLookup.GetDocumentNumbersAsync(row.SupplierId, ct))
                .Select(DocumentNumberKey.Normalize)
                .ToHashSet(StringComparer.Ordinal);
            existingKeys[row.SupplierId] = keys;
        }
        if (keys.Contains(row.DocumentKey))
            return $"el proveedor {match.LegalName} ya tiene una cuenta por pagar con el documento '{row.DocumentNumber}'.";
        return null;
    }
}

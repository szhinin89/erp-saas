using System.Text.Json;
using ERP.Application.Modules.InitialLoad.DTOs;
using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.MasterData.Enums;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Common;

namespace ERP.Application.Modules.InitialLoad.Processors;

/// <summary>
/// IL-5B — confirmación atómica de la CxC Inicial. Primero se revalida TODO el lote contra el
/// estado actual (stale preview); solo si ninguna fila cambió se crea una <c>SalesReceivable</c>
/// InitialBalance por fila (una cuota por el saldo con su vencimiento). Cualquier fallo devuelve
/// <see cref="BatchConfirmResult.Failed"/> y el handler revierte la transacción completa. Nunca
/// crea facturas, clientes ni asientos (contabilidad de la apertura → IL-7).
/// </summary>
public sealed partial class InitialReceivableImportProcessor
{
    public Task<BatchConfirmResult> ConfirmBatchAsync(
        IReadOnlyList<(int RowNumber, string ParsedDataJson)> rows, CancellationToken ct) =>
        Task.FromResult(BatchConfirmResult.Failed("La CxC inicial requiere el lote de origen para confirmarse."));

    public async Task<BatchConfirmResult> ConfirmBatchAsync(
        Guid importBatchId, IReadOnlyList<(int RowNumber, string ParsedDataJson)> rows, CancellationToken ct)
    {
        var parsed = rows
            .Select(r => (r.RowNumber, Row: JsonSerializer.Deserialize<ParsedInitialReceivableRow>(r.ParsedDataJson)!))
            .ToList();
        if (parsed.Count == 0)
            return BatchConfirmResult.Failed("El lote no tiene filas para confirmar.");

        var batchError = await RevalidateBatchAsync(parsed.Select(p => p.Row).ToList(), ct);
        if (batchError is not null)
            return BatchConfirmResult.Failed($"{batchError} Vuelva a validar el archivo.");

        var existingKeys = new Dictionary<Guid, HashSet<string>>();
        foreach (var (rowNumber, row) in parsed)
        {
            var error = await RevalidateRowAsync(row, existingKeys, ct);
            if (error is not null)
                return BatchConfirmResult.Failed($"Fila {rowNumber}: {error} Vuelva a validar el archivo.");
        }

        var created = new Dictionary<int, Guid>();
        foreach (var (rowNumber, row) in parsed)
        {
            var receivable = SalesReceivable.CreateInitialBalance(
                _ctx.TenantId,
                _ctx.CompanyId,
                row.BranchId,
                row.CustomerId,
                row.DocumentNumber,
                row.IssueDate!.Value,
                row.DueDate!.Value,
                row.Balance,
                importBatchId,
                _ctx.UserId
            );
            await _receivables.AddAsync(receivable, ct);
            created[rowNumber] = receivable.Id;
        }
        await _receivables.SaveChangesAsync(ct);
        return BatchConfirmResult.Success(created);
    }

    /// <summary>
    /// Reglas de lote: sucursal activa, un único corte igual a <c>Company.OpeningBalanceDate</c>
    /// vigente (no futuro) y ningún documento repetido dentro del lote.
    /// </summary>
    private async Task<string?> RevalidateBatchAsync(IReadOnlyList<ParsedInitialReceivableRow> rows, CancellationToken ct)
    {
        if (_branch.BranchId == Guid.Empty || rows.Any(r => r.BranchId != _branch.BranchId))
            return "El lote pertenece a otra sucursal o no hay sucursal activa.";

        var cutoffs = rows.Select(r => r.CutoffDate).Distinct().ToList();
        if (cutoffs.Count != 1 || cutoffs[0] is not { } cutoff)
            return "El lote debe tener una única fecha de corte.";
        var opening = await _openingBalance.GetOpeningBalanceDateAsync(ct);
        if (opening is null)
            return "La empresa ya no tiene definida su fecha de apertura de saldos.";
        if (opening.Value != cutoff)
            return $"La fecha de corte del lote ({cutoff:yyyy-MM-dd}) ya no coincide con la fecha de apertura "
                + $"de saldos de la empresa ({opening.Value:yyyy-MM-dd}).";
        var today = await _clock.TodayAsync(_ctx.CompanyId, _ctx.TenantId, ct);
        if (cutoff > today)
            return $"La fecha de corte {cutoff:yyyy-MM-dd} es posterior a hoy.";

        if (rows.GroupBy(r => (r.CustomerId, r.DocumentKey)).Any(g => g.Count() > 1))
            return "El lote repite un documento del mismo cliente.";
        return null;
    }

    /// <summary>Estado actual del cliente, unicidad del documento y forma de la fila.</summary>
    private async Task<string?> RevalidateRowAsync(
        ParsedInitialReceivableRow row, Dictionary<Guid, HashSet<string>> existingKeys, CancellationToken ct)
    {
        if (row.CustomerId == Guid.Empty || row.IssueDate is not { } issue || row.DueDate is not { } due
            || row.CutoffDate is not { } cutoff)
            return "la fila no está validada.";
        if (row.CurrencyCode != SupportedCurrency)
            return $"la moneda '{row.CurrencyCode}' no está admitida.";
        if (!OpeningBalanceImportRules.IsValidBalance(row.Balance))
            return "el saldo pendiente no es válido.";
        if (issue > cutoff || due < issue)
            return "las fechas de emisión/vencimiento no son coherentes con el corte.";
        if (row.DocumentKey.Length == 0 || DocumentNumberKey.Normalize(row.DocumentNumber) != row.DocumentKey)
            return "el número de documento no es válido.";

        var match = await _partnerLookup.FindByIdentificationAsync(
            row.IdentificationType, row.IdentificationNumber, RoleType.Customer, ct);
        if (match is null || match.BusinessPartnerId != row.CustomerId || match.IsAmbiguous)
            return $"el cliente {row.IdentificationNumber} ya no existe en el maestro.";
        if (!match.IsActive)
            return $"el cliente {match.LegalName} fue inactivado.";
        if (!match.HasActiveRole)
            return $"el cliente {match.LegalName} ya no tiene rol Cliente activo.";

        if (!existingKeys.TryGetValue(row.CustomerId, out var keys))
        {
            keys = (await _receivableLookup.GetDocumentNumbersAsync(row.CustomerId, ct))
                .Select(DocumentNumberKey.Normalize)
                .ToHashSet(StringComparer.Ordinal);
            existingKeys[row.CustomerId] = keys;
        }
        if (keys.Contains(row.DocumentKey))
            return $"el cliente {match.LegalName} ya tiene una cuenta por cobrar con el documento '{row.DocumentNumber}'.";
        return null;
    }
}

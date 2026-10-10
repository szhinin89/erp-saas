using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.Inventory.Enums;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.InitialLoad;

/// <inheritdoc cref="IOpeningBalanceSourceReader"/>
public sealed class OpeningBalanceSourceReader : IOpeningBalanceSourceReader
{
    // Documento de apertura de inventario que IL-4 crea por bodega (PostInitialBalanceCommand).
    private const string StockAdjustmentSourceDocType = "StockAdjustment";

    private readonly ErpDbContext _db;

    public OpeningBalanceSourceReader(ErpDbContext db) => _db = db;

    // ImportBatchRow, StockMovement, SalesReceivable y AccountsPayable son
    // ICompanyOperationalEntity: el filtro global acota tenant + empresa activa (fail-closed).
    public Task<OpeningBalanceSource> GetConfirmedSourceAsync(
        Guid importBatchId, ImportType importType, CancellationToken ct) =>
        importType switch
        {
            ImportType.InitialStock => InitialStockAsync(importBatchId, ct),
            ImportType.InitialReceivables => InitialReceivablesAsync(importBatchId, ct),
            ImportType.InitialPayables => InitialPayablesAsync(importBatchId, ct),
            _ => Task.FromResult(new OpeningBalanceSource(0, 0m, [], 0)),
        };

    /// <summary>Las filas confirmadas guardan el documento de apertura creado (uno por bodega).</summary>
    private async Task<OpeningBalanceSource> InitialStockAsync(Guid importBatchId, CancellationToken ct)
    {
        var documentIds = await _db.ImportBatchRows.AsNoTracking()
            .Where(r => r.ImportBatchId == importBatchId && r.IsImported && r.CreatedBusinessPartnerId != null)
            .Select(r => r.CreatedBusinessPartnerId!.Value)
            .Distinct()
            .ToListAsync(ct);
        if (documentIds.Count == 0)
            return new OpeningBalanceSource(0, 0m, [], 0);

        var movements = await _db.StockMovements.AsNoTracking()
            .Where(m => m.MovementType == StockMovementType.InitialBalance
                && m.SourceDocType == StockAdjustmentSourceDocType
                && m.SourceDocId != null && documentIds.Contains(m.SourceDocId.Value))
            .Select(m => new { m.TotalCost, m.EffectiveDate })
            .ToListAsync(ct);
        return new OpeningBalanceSource(
            movements.Count,
            movements.Sum(m => m.TotalCost ?? 0m),
            movements.Select(m => m.EffectiveDate).Distinct().ToList(),
            movements.Count(m => m.TotalCost is null));
    }

    /// <summary>Monto original confirmado (los cobros posteriores al corte no alteran la apertura).</summary>
    private async Task<OpeningBalanceSource> InitialReceivablesAsync(Guid importBatchId, CancellationToken ct)
    {
        var amounts = await _db.SalesReceivables.AsNoTracking()
            .Where(r => r.ImportBatchId == importBatchId && r.Origin == SalesReceivableOrigin.InitialBalance)
            .Select(r => r.OriginalAmount)
            .ToListAsync(ct);
        return new OpeningBalanceSource(amounts.Count, amounts.Sum(), [], 0);
    }

    /// <summary>Cuotas originales confirmadas (los pagos posteriores al corte no alteran la apertura).</summary>
    private async Task<OpeningBalanceSource> InitialPayablesAsync(Guid importBatchId, CancellationToken ct)
    {
        var payables = await _db.AccountsPayables.AsNoTracking()
            .Where(p => p.ImportBatchId == importBatchId && p.OriginType == AccountsPayableOriginType.InitialBalance)
            .Select(p => new { Amount = p.Installments.Sum(i => i.Amount), p.AccountingDate })
            .ToListAsync(ct);
        return new OpeningBalanceSource(
            payables.Count,
            payables.Sum(p => p.Amount),
            payables.Select(p => p.AccountingDate).Distinct().ToList(),
            0);
    }
}

using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

/// <summary>
/// Valoración operativa del costo de ventas (SaleCostObligation/Allocation). ACCOUNTING-CONTINUITY-05:
/// la contabilización del costo vive exclusivamente en AccountingPosting.
/// </summary>
public interface IInventoryCostLedger
{
    Task<IReadOnlyList<SaleCostObligation>> GetPendingObligationsAsync(Guid tenantId, Guid companyId, CancellationToken ct);

    /// <summary>Fechas de emisión distintas de las facturas con costo todavía pendiente de resolver.</summary>
    Task<IReadOnlyList<DateOnly>> GetPendingObligationInvoiceDatesAsync(Guid tenantId, Guid companyId, CancellationToken ct);

    Task LockInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<decimal?> GetCurrentInvoiceCostAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
}

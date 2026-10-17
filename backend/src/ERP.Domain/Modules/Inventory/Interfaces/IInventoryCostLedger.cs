using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

/// <summary>
/// Valoración operativa del costo de ventas (SaleCostObligation/Allocation). ACCOUNTING-CONTINUITY-05:
/// la contabilización del costo vive exclusivamente en AccountingPosting.
/// </summary>
public interface IInventoryCostLedger
{
    Task<IReadOnlyList<SaleCostObligation>> GetPendingObligationsAsync(Guid tenantId, Guid companyId, CancellationToken ct);

    /// <summary>
    /// Fecha de emisión de cada factura con costo todavía pendiente de resolver (un elemento por
    /// factura; ACCOUNTING-CONTINUITY-06 las cuenta por período).
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetPendingObligationInvoiceDatesAsync(Guid tenantId, Guid companyId, CancellationToken ct);

    Task LockInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<decimal?> GetCurrentInvoiceCostAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
}

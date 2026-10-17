using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

/// <summary>
/// Valoración operativa del costo de ventas (SaleCostObligation/Allocation). ACCOUNTING-CONTINUITY-05:
/// la contabilización del costo vive exclusivamente en AccountingPosting.
/// </summary>
public interface IInventoryCostLedger
{
    /// <summary>
    /// Obligaciones con costo pendiente y su contexto legible (factura, ítem, bodega). ACCOUNTING-12B:
    /// solo lectura; la cobertura sigue siendo automática al ingresar costo válido.
    /// </summary>
    Task<IReadOnlyList<PendingSaleCostView>> GetPendingObligationsAsync(Guid tenantId, Guid companyId, CancellationToken ct);

    /// <summary>
    /// Fecha de emisión de cada factura con costo todavía pendiente de resolver (un elemento por
    /// factura; ACCOUNTING-CONTINUITY-06 las cuenta por período).
    /// </summary>
    Task<IReadOnlyList<DateOnly>> GetPendingObligationInvoiceDatesAsync(Guid tenantId, Guid companyId, CancellationToken ct);

    Task LockInvoiceAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
    Task<decimal?> GetCurrentInvoiceCostAsync(Guid tenantId, Guid companyId, Guid invoiceId, CancellationToken ct);
}

/// <summary>Proyección de lectura de una <see cref="SaleCostObligation"/> pendiente (ACCOUNTING-12B).</summary>
public sealed record PendingSaleCostView(
    Guid ObligationId, Guid InvoiceId, Guid InvoiceLineId, string? InvoiceNumber, DateOnly? IssueDate,
    Guid ProductId, string? ItemSku, string? ItemName, Guid WarehouseId, string? WarehouseName,
    decimal PendingQuantity, decimal? ProvisionalUnitCost);

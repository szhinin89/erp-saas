using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Events;

public sealed record PurchBillApprovedStockLine(
    Guid    BillLineId,
    Guid?   ProductId,
    Guid    WarehouseId,
    decimal Quantity,
    decimal NetUnitCost);

public sealed class PurchBillApprovedEvent : IDomainEvent
{
    public Guid     Id              { get; } = Guid.NewGuid();
    public DateTime OccurredOn      { get; } = DateTime.UtcNow;
    public Guid     PurchBillId     { get; }
    public Guid     TenantId        { get; }
    public string   InvoiceNumber   { get; }
    public Guid     ApprovedByUserId { get; }
    public IReadOnlyList<PurchBillApprovedStockLine> StockLines { get; }

    public PurchBillApprovedEvent(
        Guid    purchBillId,
        Guid    tenantId,
        string  invoiceNumber,
        Guid    approvedByUserId,
        IReadOnlyList<PurchBillApprovedStockLine> stockLines)
    {
        PurchBillId      = purchBillId;
        TenantId         = tenantId;
        InvoiceNumber    = invoiceNumber;
        ApprovedByUserId = approvedByUserId;
        StockLines       = stockLines;
    }
}

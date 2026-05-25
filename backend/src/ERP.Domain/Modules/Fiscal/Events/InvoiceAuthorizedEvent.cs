using ERP.Domain.Common;
using ERP.Domain.Modules.Fiscal.Entities;

namespace ERP.Domain.Modules.Fiscal.Events;

public sealed class InvoiceAuthorizedEvent : BaseDomainEvent
{
    public long InvoiceId { get; }
    public Guid InvoicePublicId { get; }
    public Guid SubscriberId { get; }
    public Guid UserId { get; }
    public Guid WarehouseId { get; }
    public Guid BranchId { get; }
    public string InvoiceNumber { get; }
    public IReadOnlyList<InvoiceAuthorizedStockLine> StockLines { get; }

    public InvoiceAuthorizedEvent(
        long invoiceId,
        Guid invoicePublicId,
        Guid subscriberId,
        Guid userId,
        Guid warehouseId,
        Guid branchId,
        string invoiceNumber,
        IReadOnlyList<InvoiceAuthorizedStockLine> stockLines)
    {
        InvoiceId = invoiceId;
        InvoicePublicId = invoicePublicId;
        SubscriberId = subscriberId;
        UserId = userId;
        WarehouseId = warehouseId;
        BranchId = branchId;
        InvoiceNumber = invoiceNumber;
        StockLines = stockLines;
        SubscriberId = subscriberId;
    }
}

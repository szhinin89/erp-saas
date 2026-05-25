using ERP.Domain.Common;

namespace ERP.Domain.Modules.Fiscal.Events;

public sealed class InvoiceCreatedEvent : BaseDomainEvent
{
    public long InvoiceId { get; }
    public Guid InvoicePublicId { get; }
    public Guid SubscriberId { get; }
    public Guid BranchId { get; }
    public Guid UserId { get; }

    public InvoiceCreatedEvent(long invoiceId, Guid invoicePublicId, Guid subscriberId, Guid branchId, Guid userId)
    {
        InvoiceId = invoiceId;
        InvoicePublicId = invoicePublicId;
        SubscriberId = subscriberId;
        BranchId = branchId;
        UserId = userId;
        SubscriberId = subscriberId;
    }
}

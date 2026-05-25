using ERP.Domain.Common;

namespace ERP.Domain.Modules.Commercial.Events;

public sealed class QuoteCreatedEvent : BaseDomainEvent
{
    public long QuoteId { get; }
    public Guid QuotePublicId { get; }
    public Guid SubscriberId { get; }
    public Guid BranchId { get; }
    public Guid UserId { get; }

    public QuoteCreatedEvent(
        long quoteId,
        Guid quotePublicId,
        Guid subscriberId,
        Guid branchId,
        Guid userId)
    {
        QuoteId = quoteId;
        QuotePublicId = quotePublicId;
        SubscriberId = subscriberId;
        BranchId = branchId;
        UserId = userId;
        SubscriberId = subscriberId;
    }
}

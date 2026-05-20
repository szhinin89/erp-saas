using ERP.Domain.Common;

namespace ERP.Domain.Modules.Accounting.Events;

public sealed record JournalEntryCreatedEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid JournalEntryId { get; }
    public Guid SubscriberId { get; }

    public JournalEntryCreatedEvent(Guid journalEntryId, Guid subscriberId)
    {
        JournalEntryId = journalEntryId;
        SubscriberId       = subscriberId;
    }
}

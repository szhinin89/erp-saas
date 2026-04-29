using ERP.Domain.Common;

namespace ERP.Domain.Accounting.Events;

public sealed record JournalEntryCreatedEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
    public Guid JournalEntryId { get; }
    public Guid TenantId { get; }

    public JournalEntryCreatedEvent(Guid journalEntryId, Guid tenantId)
    {
        JournalEntryId = journalEntryId;
        TenantId       = tenantId;
    }
}

namespace ERP.Domain.Common;

public abstract class SystemAggregateRoot : SystemBaseEntity, IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent eventItem) => _domainEvents.Add(eventItem);

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

namespace ERP.Domain.Common;

/// <summary>
/// Raíz de agregado transaccional greenfield: PK <see cref="Id"/> (Snowflake) + <see cref="PublicId"/> (UUID API).
/// </summary>
public abstract class SnowflakeAggregateRoot : IHasDomainEvents, ITenantScopedEntity, ICompanyOperationalEntity
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public long Id { get; protected set; }
    public Guid PublicId { get; protected set; }
    public Guid TenantId { get; protected set; }
    public Guid CompanyId { get; protected set; }
    public long RowVersion { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void AddDomainEvent(IDomainEvent eventItem) => _domainEvents.Add(eventItem);

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void BumpRowVersion() => RowVersion++;
}

namespace ERP.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid TenantId { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(Guid id, Guid tenantId)
    {
        Id = id;
        TenantId = tenantId;
    }
}

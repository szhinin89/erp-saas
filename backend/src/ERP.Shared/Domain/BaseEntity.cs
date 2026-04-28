namespace ERP.Shared.Domain;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public Guid TenantId { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public bool IsActive { get; protected set; } = true;

    protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;
    protected void Deactivate() => IsActive = false;
}

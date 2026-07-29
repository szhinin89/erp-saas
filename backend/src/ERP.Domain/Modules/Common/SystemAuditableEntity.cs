namespace ERP.Domain.Common;

public abstract class SystemAuditableEntity : SystemAggregateRoot
{
    public DateTime CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }
    public Guid CreatedBy { get; protected set; }
    public Guid? UpdatedBy { get; protected set; }

    protected void SetCreated(Guid userId)
    {
        CreatedAt = DateTime.UtcNow;
        CreatedBy = userId;
    }

    protected void SetUpdated(Guid userId)
    {
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = userId;
    }
}

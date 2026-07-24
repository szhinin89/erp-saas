namespace ERP.Domain.Common;

/// <summary>
/// Auditoría estándar para mutaciones con actor de aplicación (<c>CreatedBy</c> / <c>UpdatedBy</c>).
/// Las entidades multi-tenant puramente técnicas (jobs, snapshots) pueden implementar solo
/// <see cref="ITenantScopedEntity"/>; ver backlog P4 (auditoría de dominio).
/// </summary>
public abstract class AuditableEntity : AggregateRoot
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

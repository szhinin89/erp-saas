using ERP.Domain.Common;

namespace ERP.Domain.Audit.Entities;

/// <summary>
/// Registro append-only de acciones realizadas por un usuario (auditoría / historial).
/// </summary>
public class UserActivity : SystemBaseEntity, IMustHaveTenant
{
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string? UserEmail { get; private set; }
    public string? UserFullName { get; private set; }

    public string Module { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? Description { get; private set; }

    public DateTime CreatedAt { get; private set; }

    private UserActivity() { }

    public static UserActivity Create(
        Guid tenantId,
        Guid userId,
        string? userEmail,
        string? userFullName,
        string module,
        string action,
        string? entityType,
        Guid? entityId,
        string? description
    )
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenantId requerido.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId requerido.", nameof(userId));
        if (string.IsNullOrWhiteSpace(module))
            throw new ArgumentException("Module requerido.", nameof(module));
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action requerida.", nameof(action));

        return new UserActivity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            UserEmail = string.IsNullOrWhiteSpace(userEmail) ? null : userEmail.Trim(),
            UserFullName = string.IsNullOrWhiteSpace(userFullName) ? null : userFullName.Trim(),
            Module = module.Trim(),
            Action = action.Trim(),
            EntityType = string.IsNullOrWhiteSpace(entityType) ? null : entityType.Trim(),
            EntityId = entityId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = DateTime.UtcNow,
        };
    }
}

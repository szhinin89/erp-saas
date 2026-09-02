using ERP.Domain.Kernel.Security;

namespace ERP.Domain.Access.Entities;

public sealed class GlobalUserRole
{
    private GlobalUserRole() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static GlobalUserRole Create(Guid userId, string role, Guid createdBy)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("El usuario es obligatorio.", nameof(userId));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("El rol es obligatorio.", nameof(role));

        if (!string.Equals(role, SecurityRoles.Admin, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Rol global no permitido.", nameof(role));

        return new GlobalUserRole
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Role = SecurityRoles.Admin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    public void Deactivate(Guid updatedBy)
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}

using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Perfil de acceso (por tenant) para agrupar permisos/roles reutilizables.
/// </summary>
public class AccessProfile : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    private AccessProfile() { }

    public static AccessProfile Create(Guid tenantId, string name, string? description, Guid createdBy)
    {
        var p = new AccessProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true
        };
        p.SetCreated(createdBy);
        return p;
    }

    public void Update(string name, string? description, Guid updatedBy)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SetUpdated(updatedBy);
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(Guid updatedBy)
    {
        IsActive = true;
        SetUpdated(updatedBy);
    }
}


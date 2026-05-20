using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Permiso asignado a un perfil (por tenant).
/// PermissionKey es una cadena estable (ej. "inventario.brands.view").
/// </summary>
public class AccessProfilePermission : AuditableEntity
{
    public Guid ProfileId { get; private set; }
    public string PermissionKey { get; private set; } = null!;
    public bool IsAllowed { get; private set; }

    private AccessProfilePermission() { }

    public static AccessProfilePermission Create(
        Guid subscriberId,
        Guid profileId,
        string permissionKey,
        bool isAllowed,
        Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            throw new ArgumentException("PermissionKey requerido.", nameof(permissionKey));

        var p = new AccessProfilePermission
        {
            Id = Guid.NewGuid(),
            SubscriberId = subscriberId,
            ProfileId = profileId,
            PermissionKey = permissionKey.Trim(),
            IsAllowed = isAllowed,
        };

        p.SetCreated(createdBy);
        return p;
    }

    public void SetAllowed(bool isAllowed, Guid updatedBy)
    {
        IsAllowed = isAllowed;
        SetUpdated(updatedBy);
    }
}


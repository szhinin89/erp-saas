// CA1711: Name reflects business domain semantics; renaming would break EF mappings, DTOs and handlers.
#pragma warning disable CA1711
using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Permiso asignado a un perfil. La empresa la determina el <see cref="AccessProfile"/> dueño
/// (SEC-05): toda lectura/escritura resuelve antes el perfil en el tenant y empresa validados.
/// PermissionKey es una cadena estable (ej. "inventario.brands.view").
/// </summary>
public class AccessProfilePermission : AuditableEntity
{
    public Guid ProfileId { get; private set; }
    public string PermissionKey { get; private set; } = null!;
    public bool IsAllowed { get; private set; }

    private AccessProfilePermission() { }

    public static AccessProfilePermission Create(
        Guid tenantId,
        Guid profileId,
        string permissionKey,
        bool isAllowed,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
            throw new ArgumentException("PermissionKey requerido.", nameof(permissionKey));

        var p = new AccessProfilePermission
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
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

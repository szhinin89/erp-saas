using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Perfil de acceso de una empresa (<c>TenantId + CompanyId</c>) para agrupar permisos
/// reutilizables. SEC-05: cada empresa tiene sus propios perfiles — dos empresas pueden usar el
/// mismo nombre sin compartir identidad ni permisos, y una membership solo puede recibir perfiles
/// de su misma empresa. Un perfil inactivo no concede permisos efectivos.
/// </summary>
public class AccessProfile : AuditableEntity, ICompanyOperationalEntity
{
    public Guid CompanyId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }

    private AccessProfile() { }

    public static AccessProfile Create(
        Guid tenantId,
        Guid companyId,
        string name,
        string? description,
        Guid createdBy
    )
    {
        if (companyId == Guid.Empty)
            throw new ArgumentException("CompanyId requerido.", nameof(companyId));

        var p = new AccessProfile
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            IsActive = true,
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

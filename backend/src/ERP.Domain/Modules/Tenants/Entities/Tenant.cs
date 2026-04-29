using ERP.Domain.Common;

namespace ERP.Domain.Tenants.Entities;

public class Tenant : AuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private Tenant() { }

    public static Tenant Create(string name, string slug, Guid createdBy)
    {
        var tenant = new Tenant
        {
            Id       = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Name     = name,
            Slug     = slug.ToLowerInvariant(),
            IsActive = true
        };
        tenant.SetCreated(createdBy);
        return tenant;
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }
}

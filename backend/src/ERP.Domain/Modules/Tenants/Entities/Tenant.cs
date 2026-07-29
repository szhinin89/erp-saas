using ERP.Domain.Common;

namespace ERP.Domain.Tenants.Entities;

public sealed class Tenant : SystemAuditableEntity
{
    public string Name { get; private set; } = null!;
    public string Slug { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public string PreferredLanguage { get; private set; } = "es";

    private Tenant() { }

    public static Tenant Create(
        string name,
        string slug,
        Guid createdBy,
        string preferredLanguage = "es")
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            IsActive = true,
            PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "es" : preferredLanguage.Trim().ToLowerInvariant(),
        };
        tenant.SetCreated(createdBy);
        return tenant;
    }

    public void UpdateProfile(string name, string slug, string? preferredLanguage, Guid updatedBy)
    {
        Name = name;
        Slug = slug.ToLowerInvariant();
        PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "es" : preferredLanguage!.Trim().ToLowerInvariant();
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

using ERP.Domain.Common;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Relación de acceso entre un usuario global y una empresa (tenant).
/// Esta entidad es multi-tenant: su TenantId es la empresa a la que otorga acceso.
/// </summary>
public class Membership : AuditableEntity
{
    public Guid IdentityUserId { get; private set; }
    public string Role { get; private set; } = null!;
    public Guid? ProfileId { get; private set; }
    public bool IsActive { get; private set; }

    private Membership() { }

    public static Membership Create(
        Guid tenantId,
        Guid identityUserId,
        string role,
        Guid? profileId,
        Guid createdBy)
    {
        var m = new Membership
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdentityUserId = identityUserId,
            Role = role,
            ProfileId = profileId,
            IsActive = true
        };

        m.SetCreated(createdBy);
        return m;
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void Activate(string role, Guid? profileId, Guid updatedBy)
    {
        Role = role;
        ProfileId = profileId;
        IsActive = true;
        SetUpdated(updatedBy);
    }
}


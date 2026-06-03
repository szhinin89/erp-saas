namespace ERP.Domain.Access.Entities;

/// <summary>
/// Acceso de un <see cref="IdentityUser"/> a una empresa operativa (<c>company</c>).
/// Scope ERP: <c>company_id</c>. No usar <c>subscriber_id</c> en esta entidad.
/// </summary>
public sealed class CompanyUserMembership
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid IdentityUserId { get; private set; }
    public string Role { get; private set; } = null!;
    public Guid? ProfileId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public Guid UpdatedBy { get; private set; }

    private CompanyUserMembership() { }

    public static CompanyUserMembership Create(
        Guid companyId,
        Guid identityUserId,
        string role,
        Guid? profileId,
        Guid createdBy)
    {
        var now = DateTime.UtcNow;
        return new CompanyUserMembership
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            IdentityUserId = identityUserId,
            Role = role,
            ProfileId = profileId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
        };
    }

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void Activate(string role, Guid? profileId, Guid updatedBy)
    {
        Role = role;
        ProfileId = profileId;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}

using ERP.Domain.Common;
using ERP.Domain.Auth.ValueObjects;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Usuario global del sistema (no se duplica por empresa).
/// El acceso a empresas se define mediante CompanyUserMemberships.
/// </summary>
public class IdentityUser : SystemAuditableEntity
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private IdentityUser() { }

    public static IdentityUser Create(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        Guid createdBy)
    {
        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = lastName,
            Email = new Email(email),
            PasswordHash = passwordHash,
            IsActive = true
        };

        user.SetCreated(createdBy);
        return user;
    }

    public string FullName => $"{FirstName} {LastName}";

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        SetUpdated(updatedBy);
    }

    public void SetPasswordHash(string passwordHash, Guid updatedBy)
    {
        PasswordHash = passwordHash;
        SetUpdated(updatedBy);
    }
}


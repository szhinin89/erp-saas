using ERP.Domain.Common;
using ERP.Domain.Auth.ValueObjects;

namespace ERP.Domain.Auth.Entities;

public class User : AuditableEntity
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string Role { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private User() { }

    public static User Create(
        Guid tenantId,
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        string role,
        Guid createdBy)
    {
        var user = new User
        {
            Id           = Guid.NewGuid(),
            TenantId     = tenantId,
            FirstName    = firstName,
            LastName     = lastName,
            Email        = new Email(email),
            PasswordHash = passwordHash,
            Role         = role,
            IsActive     = true
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

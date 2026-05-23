using ERP.Domain.Access.Enums;
using ERP.Domain.Common;
using ERP.Domain.Auth.ValueObjects;

namespace ERP.Domain.Access.Entities;

/// <summary>
/// Usuario global del sistema (auth único). Platform operators y usuarios ERP comparten esta tabla.
/// Acceso operativo ERP vía <see cref="CompanyUserMembership"/>.
/// </summary>
public class IdentityUser : SystemAuditableEntity
{
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public Email Email { get; private set; } = null!;
    public string EmailNormalized { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public IdentityUserType UserType { get; private set; }
    public PlatformRole? PlatformRole { get; private set; }
    /// <summary>
    /// Legado: asociación directa usuario→subscriber para usuarios single-tenant.
    /// En arquitectura multi-empresa la relación canónica es:
    /// IdentityUser → CompanyUserMembership → Company → Subscriber.
    /// No usar en guards, policies ni handlers de autorización nuevos.
    /// </summary>
    [Obsolete("Use CompanyUserMembership chain instead. Kept for backward compat with single-tenant user records.")]
    public Guid? SubscriberId { get; private set; }
    public string SecurityStamp { get; private set; } = null!;
    public bool RequirePasswordReset { get; private set; }

    private IdentityUser() { }

    public static IdentityUser Create(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        Guid createdBy)
        => CreateCompanyUser(firstName, lastName, email, passwordHash, createdBy);

    public static IdentityUser CreateCompanyUser(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        Guid createdBy,
        Guid? subscriberId = null)
    {
        var normalized = NormalizeEmail(email);
        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = new Email(normalized),
            EmailNormalized = normalized,
            PasswordHash = passwordHash,
            IsActive = true,
            UserType = IdentityUserType.Company,
            PlatformRole = null,
            SubscriberId = subscriberId,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            RequirePasswordReset = false,
        };
        user.SetCreated(createdBy);
        return user;
    }

    public static IdentityUser CreatePlatformSuperAdmin(
        string firstName,
        string lastName,
        string email,
        string passwordHash,
        Guid createdBy,
        bool requirePasswordReset = false)
    {
        var normalized = NormalizeEmail(email);
        var user = new IdentityUser
        {
            Id = Guid.NewGuid(),
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Email = new Email(normalized),
            EmailNormalized = normalized,
            PasswordHash = passwordHash,
            IsActive = true,
            UserType = IdentityUserType.Platform,
            PlatformRole = Enums.PlatformRole.SuperAdmin,
            SubscriberId = null,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            RequirePasswordReset = requirePasswordReset,
        };
        user.SetCreated(createdBy);
        return user;
    }

    public bool IsPlatformSuperAdmin =>
        UserType == IdentityUserType.Platform
        && PlatformRole == Enums.PlatformRole.SuperAdmin
        && IsActive;

    public string FullName => $"{FirstName} {LastName}";

    public void Deactivate(Guid updatedBy)
    {
        IsActive = false;
        BumpSecurityStamp(updatedBy);
    }

    public void SetPasswordHash(string passwordHash, Guid updatedBy)
    {
        PasswordHash = passwordHash;
        RequirePasswordReset = false;
        BumpSecurityStamp(updatedBy);
        SetUpdated(updatedBy);
    }

    public void MarkRequirePasswordReset(Guid updatedBy)
    {
        RequirePasswordReset = true;
        BumpSecurityStamp(updatedBy);
    }

    private void BumpSecurityStamp(Guid updatedBy)
    {
        SecurityStamp = Guid.NewGuid().ToString("N");
        SetUpdated(updatedBy);
    }

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}

namespace ERP.Domain.Kernel.Security;

/// <summary>
/// Single source of truth for JWT role claim values (<c>ClaimTypes.Role</c>).
/// Not to be confused with permission keys (<see cref="ERP.Domain.Kernel.Permissions"/>)
/// or access profiles (e.g. "DataEntry"), which are assignable and tenant-scoped.
/// </summary>
public static class SecurityRoles
{
    /// <summary>Platform/tenant administrator. Bypasses all permission checks (wildcard `*`).</summary>
    public const string Admin = "Admin";

    /// <summary>Default role for a user not yet assigned an active company/profile.</summary>
    public const string User = "User";
}

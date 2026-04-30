using ERP.Domain.Access.Entities;

namespace ERP.Domain.Access.Interfaces;

/// <summary>
/// Repositorio del módulo Access (Identity and Access Management).
/// Inspirado en el patrón de IAccountingRepository (métodos explícitos, unit of work vía SaveChangesAsync).
/// </summary>
public interface IAccessRepository
{
    // ── Usuarios globales ──────────────────────────────────────────
    Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default);
    Task<IdentityUser?> GetUserByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnyUserWithEmailAsync(string email, CancellationToken ct = default);
    Task AddUserAsync(IdentityUser user, CancellationToken ct = default);

    // ── Memberships (acceso por tenant) ────────────────────────────
    Task<IReadOnlyList<Membership>> GetActiveMembershipsForUserSystemAsync(Guid identityUserId, CancellationToken ct = default);
    Task<Membership?> GetMembershipAsync(Guid tenantId, Guid identityUserId, CancellationToken ct = default);
    Task AddMembershipAsync(Membership membership, CancellationToken ct = default);

    Task<IReadOnlyList<Membership>> GetMembershipsByTenantAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);

    // ── Perfiles (tenant) ───────────────────────────────────────────
    Task<IReadOnlyList<AccessProfile>> GetProfilesByTenantAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default);
    Task<AccessProfile?> GetProfileByIdAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);
    Task AddProfileAsync(AccessProfile profile, CancellationToken ct = default);

    // ── Permisos por perfil (tenant) ───────────────────────────────
    Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);
    Task<AccessProfilePermission?> GetProfilePermissionAsync(Guid tenantId, Guid profileId, string permissionKey, CancellationToken ct = default);
    Task AddProfilePermissionAsync(AccessProfilePermission permission, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}


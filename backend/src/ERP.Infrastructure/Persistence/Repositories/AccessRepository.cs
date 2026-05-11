using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

/// <summary>
/// Memberships implementan <c>ITenantEntity</c>; los métodos que cruzan empresas o fijan <c>TenantId</c>
/// en el predicado usan <c>IgnoreQueryFilters()</c> (EF Core) a propósito.
/// </summary>
public class AccessRepository : IAccessRepository
{
    private readonly ErpDbContext _db;

    public AccessRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
        => _db.IdentityUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task<IdentityUser?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return _db.IdentityUsers.FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

    public Task<bool> AnyUserWithEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = new Email(email);
        return _db.IdentityUsers.AnyAsync(u => u.Email == normalized, ct);
    }

    public Task<int> CountIdentityUsersAsync(CancellationToken ct = default)
        => _db.IdentityUsers.CountAsync(ct);

    public Task AddUserAsync(IdentityUser user, CancellationToken ct = default)
        => _db.IdentityUsers.AddAsync(user, ct).AsTask();

    /// <summary>Bootstrap IAM: todas las membresías activas del usuario (IQF + <c>IdentityUserId</c>).</summary>
    public async Task<IReadOnlyList<Membership>> GetActiveMembershipsForUserSystemAsync(Guid identityUserId, CancellationToken ct = default)
    {
        // Necesario para listar todas las empresas del usuario (cross-tenant).
        return await _db.Memberships
            .IgnoreQueryFilters()
            .Where(m => m.IdentityUserId == identityUserId && m.IsActive)
            .ToListAsync(ct);
    }

    /// <summary>Par empresa + usuario; <c>TenantId</c> explícito en el predicado.</summary>
    public Task<Membership?> GetMembershipAsync(Guid tenantId, Guid identityUserId, CancellationToken ct = default)
        => _db.Memberships
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.IdentityUserId == identityUserId, ct);

    public Task AddMembershipAsync(Membership membership, CancellationToken ct = default)
        => _db.Memberships.AddAsync(membership, ct).AsTask();

    public async Task<IReadOnlyList<Membership>> GetMembershipsByTenantAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default)
    {
        // Para administración dentro del tenant actual.
        var q = _db.Memberships.Where(m => m.TenantId == tenantId);
        if (onlyActive) q = q.Where(m => m.IsActive);
        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(ct);
    }

    /// <summary>Cupo por empresa: IQF + <c>TenantId</c> explícito (no depender solo del filtro global).</summary>
    public Task<int> CountActiveMembershipsByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Memberships.IgnoreQueryFilters()
            .CountAsync(m => m.TenantId == tenantId && m.IsActive, ct);

    public async Task<IReadOnlyList<AccessProfile>> GetProfilesByTenantAsync(Guid tenantId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _db.AccessProfiles.Where(p => p.TenantId == tenantId);
        if (onlyActive) q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<AccessProfile?> GetProfileByIdAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => _db.AccessProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == profileId, ct);

    public Task AddProfileAsync(AccessProfile profile, CancellationToken ct = default)
        => _db.AccessProfiles.AddAsync(profile, ct).AsTask();

    public async Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => await _db.AccessProfilePermissions
            .Where(p => p.TenantId == tenantId && p.ProfileId == profileId)
            .OrderBy(p => p.PermissionKey)
            .ToListAsync(ct);

    public Task<AccessProfilePermission?> GetProfilePermissionAsync(Guid tenantId, Guid profileId, string permissionKey, CancellationToken ct = default)
        => _db.AccessProfilePermissions
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.ProfileId == profileId && p.PermissionKey == permissionKey, ct);

    public Task AddProfilePermissionAsync(AccessProfilePermission permission, CancellationToken ct = default)
        => _db.AccessProfilePermissions.AddAsync(permission, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}


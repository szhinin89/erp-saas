using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public class AccessRepository : IAccessRepository
{
    private readonly ErpDbContext _db;
    private readonly IPlatformQueryAccessor _platform;

    public AccessRepository(ErpDbContext db, IPlatformQueryAccessor platform)
    {
        _db = db;
        _platform = platform;
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

    public async Task<IReadOnlyList<CompanyUserMembership>> GetActiveCompanyUserMembershipsForUserSystemAsync(
        Guid identityUserId, CancellationToken ct = default)
        => await _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.CrossTenantSystem)
            .Where(m => m.IdentityUserId == identityUserId && m.IsActive)
            .ToListAsync(ct);

    public Task<CompanyUserMembership?> GetCompanyUserMembershipAsync(
        Guid companyId, Guid identityUserId, CancellationToken ct = default)
        => _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.CrossTenantSystem)
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.IdentityUserId == identityUserId, ct);

    public Task AddCompanyUserMembershipAsync(CompanyUserMembership membership, CancellationToken ct = default)
        => _db.CompanyUserMemberships.AddAsync(membership, ct).AsTask();

    public async Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByCompanyAsync(
        Guid companyId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.TenantScopedExplicit)
            .Where(m => m.CompanyId == companyId);
        if (onlyActive) q = q.Where(m => m.IsActive);
        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsBySubscriberAsync(
        Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q =
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.TenantScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId
            select m;

        if (onlyActive)
            q = q.Where(m => m.IsActive);

        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(ct);
    }

    public Task<int> CountActiveCompanyUserMembershipsByCompanyAsync(Guid companyId, CancellationToken ct = default)
        => _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.TenantScopedExplicit)
            .CountAsync(m => m.CompanyId == companyId && m.IsActive, ct);

    public Task<int> CountActiveCompanyUserMembershipsBySubscriberAsync(Guid subscriberId, CancellationToken ct = default)
        => (
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.TenantScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId && m.IsActive
            select m
        ).CountAsync(ct);

    public async Task<IReadOnlyList<AccessProfile>> GetProfilesByTenantAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _db.AccessProfiles.Where(p => p.SubscriberId == subscriberId);
        if (onlyActive) q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<AccessProfile?> GetProfileByIdAsync(Guid subscriberId, Guid profileId, CancellationToken ct = default)
        => _db.AccessProfiles.FirstOrDefaultAsync(p => p.SubscriberId == subscriberId && p.Id == profileId, ct);

    public Task AddProfileAsync(AccessProfile profile, CancellationToken ct = default)
        => _db.AccessProfiles.AddAsync(profile, ct).AsTask();

    public async Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(
        Guid subscriberId, Guid profileId, CancellationToken ct = default)
        => await _db.AccessProfilePermissions
            .Where(p => p.SubscriberId == subscriberId && p.ProfileId == profileId)
            .OrderBy(p => p.PermissionKey)
            .ToListAsync(ct);

    public Task<AccessProfilePermission?> GetProfilePermissionAsync(
        Guid subscriberId, Guid profileId, string permissionKey, CancellationToken ct = default)
        => _db.AccessProfilePermissions
            .FirstOrDefaultAsync(p => p.SubscriberId == subscriberId && p.ProfileId == profileId && p.PermissionKey == permissionKey, ct);

    public Task AddProfilePermissionAsync(AccessProfilePermission permission, CancellationToken ct = default)
        => _db.AccessProfilePermissions.AddAsync(permission, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

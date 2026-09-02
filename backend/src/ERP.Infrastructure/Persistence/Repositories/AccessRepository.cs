using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories;

public class AccessRepository : IAccessRepository
{
    private readonly ErpDbContext _db;

    public AccessRepository(ErpDbContext db)
    {
        _db = db;
    }

    public Task<int> CountActiveCompanyUsersAsync(CancellationToken cancellationToken = default) =>
        _db.IdentityUsers.CountAsync(u => u.IsActive, cancellationToken);

    public Task<IdentityUser?> GetUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    ) => _db.IdentityUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    public async Task<IReadOnlyList<IdentityUser>> GetUsersByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default
    )
    {
        if (userIds.Count == 0)
            return Array.Empty<IdentityUser>();
        return await _db
            .IdentityUsers.Where(u => userIds.Contains(u.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<IdentityUser?> GetUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = NormalizeEmail(email);
        return _db.IdentityUsers.FirstOrDefaultAsync(
            u => u.EmailNormalized == normalized,
            cancellationToken
        );
    }

    public Task<bool> AnyUserWithEmailAsync(
        string email,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = NormalizeEmail(email);
        return _db.IdentityUsers.AnyAsync(u => u.EmailNormalized == normalized, cancellationToken);
    }

    public Task<IdentityUser?> GetUserByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = NormalizeUsername(username);
        return _db.IdentityUsers.FirstOrDefaultAsync(
            u => u.UsernameNormalized == normalized,
            cancellationToken
        );
    }

    public Task<bool> AnyUserWithUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = NormalizeUsername(username);
        return _db.IdentityUsers.AnyAsync(
            u => u.UsernameNormalized == normalized,
            cancellationToken
        );
    }

    public Task<int> CountIdentityUsersAsync(CancellationToken cancellationToken = default) =>
        _db.IdentityUsers.CountAsync(cancellationToken);

    public Task AddUserAsync(IdentityUser user, CancellationToken cancellationToken = default) =>
        _db.IdentityUsers.AddAsync(user, cancellationToken).AsTask();

    public Task AddGlobalUserRoleAsync(
        GlobalUserRole role,
        CancellationToken cancellationToken = default
    ) => _db.GlobalUserRoles.AddAsync(role, cancellationToken).AsTask();

    public Task<GlobalUserRole?> GetActiveGlobalUserRoleAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default
    ) =>
        _db.GlobalUserRoles.FirstOrDefaultAsync(
            r => r.UserId == userId && r.Role == role && r.IsActive,
            cancellationToken
        );

    public async Task<
        IReadOnlyList<CompanyUserMembership>
    > GetActiveCompanyUserMembershipsForUserSystemAsync(
        Guid identityUserId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .CompanyUserMemberships.IgnoreQueryFilters()
            .Where(m => m.IdentityUserId == identityUserId && m.IsActive)
            .ToListAsync(cancellationToken);

    public Task<CompanyUserMembership?> GetCompanyUserMembershipAsync(
        Guid companyId,
        Guid identityUserId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .CompanyUserMemberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                m => m.CompanyId == companyId && m.IdentityUserId == identityUserId,
                cancellationToken
            );

    public Task<CompanyUserMembership?> GetCompanyUserMembershipByIdAsync(
        Guid companyUserMembershipId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .CompanyUserMemberships.IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.Id == companyUserMembershipId, cancellationToken);

    public Task AddCompanyUserMembershipAsync(
        CompanyUserMembership membership,
        CancellationToken cancellationToken = default
    ) => _db.CompanyUserMemberships.AddAsync(membership, cancellationToken).AsTask();

    public async Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByCompanyAsync(
        Guid companyId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db
            .CompanyUserMemberships.IgnoreQueryFilters()
            .Where(m => m.CompanyId == companyId);
        if (onlyActive)
            q = q.Where(m => m.IsActive);
        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByTenantAsync(
        Guid tenantId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var q =
            from m in _db.CompanyUserMemberships.IgnoreQueryFilters()
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.TenantId == tenantId
            select m;

        if (onlyActive)
            q = q.Where(m => m.IsActive);

        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(cancellationToken);
    }

    public Task<int> CountActiveCompanyUserMembershipsByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        _db
            .CompanyUserMemberships.IgnoreQueryFilters()
            .CountAsync(m => m.CompanyId == companyId && m.IsActive, cancellationToken);

    public Task<int> CountActiveCompanyUserMembershipsByTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) =>
        (
            from m in _db.CompanyUserMemberships.IgnoreQueryFilters()
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.TenantId == tenantId && m.IsActive
            select m
        ).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<AccessProfile>> GetProfilesByTenantAsync(
        Guid tenantId,
        bool onlyActive = true,
        CancellationToken cancellationToken = default
    )
    {
        var q = _db.AccessProfiles.Where(p => p.TenantId == tenantId);
        if (onlyActive)
            q = q.Where(p => p.IsActive);
        return await q.OrderBy(p => p.Name).ToListAsync(cancellationToken);
    }

    public Task<AccessProfile?> GetProfileByIdAsync(
        Guid tenantId,
        Guid profileId,
        CancellationToken cancellationToken = default
    ) =>
        _db.AccessProfiles.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Id == profileId,
            cancellationToken
        );

    public Task AddProfileAsync(
        AccessProfile profile,
        CancellationToken cancellationToken = default
    ) => _db.AccessProfiles.AddAsync(profile, cancellationToken).AsTask();

    public async Task<IReadOnlyList<AccessProfilePermission>> GetProfilePermissionsAsync(
        Guid tenantId,
        Guid profileId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .AccessProfilePermissions.Where(p => p.TenantId == tenantId && p.ProfileId == profileId)
            .OrderBy(p => p.PermissionKey)
            .ToListAsync(cancellationToken);

    public Task<AccessProfilePermission?> GetProfilePermissionAsync(
        Guid tenantId,
        Guid profileId,
        string permissionKey,
        CancellationToken cancellationToken = default
    ) =>
        _db.AccessProfilePermissions.FirstOrDefaultAsync(
            p =>
                p.TenantId == tenantId
                && p.ProfileId == profileId
                && p.PermissionKey == permissionKey,
            cancellationToken
        );

    public Task AddProfilePermissionAsync(
        AccessProfilePermission permission,
        CancellationToken cancellationToken = default
    ) => _db.AccessProfilePermissions.AddAsync(permission, cancellationToken).AsTask();

    public async Task<(int TotalUsers, int ActiveUsers)> CountDistinctIdentityUsersForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var userIds = await (
            from m in _db.CompanyUserMemberships.IgnoreQueryFilters()
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.TenantId == tenantId
            select m.IdentityUserId
        )
            .Distinct()
            .ToListAsync(cancellationToken);

        if (userIds.Count == 0)
            return (0, 0);

        var users = await _db
            .IdentityUsers.Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.IsActive })
            .ToListAsync(cancellationToken);

        var activeMembershipUserIds = await (
            from m in _db.CompanyUserMemberships.IgnoreQueryFilters()
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.TenantId == tenantId && m.IsActive
            select m.IdentityUserId
        )
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeSet = activeMembershipUserIds.ToHashSet();
        var activeUsers = users.Count(u => u.IsActive && activeSet.Contains(u.Id));
        return (users.Count, activeUsers);
    }

    public async Task<IReadOnlyList<IdentityUser>> GetActiveIdentityUsersForTenantAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var userIds = await (
            from m in _db.CompanyUserMemberships.IgnoreQueryFilters()
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.TenantId == tenantId && m.IsActive
            select m.IdentityUserId
        )
            .Distinct()
            .ToListAsync(cancellationToken);

        return await _db
            .IdentityUsers.Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();
}

using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
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

    public Task<IdentityUser?> GetPlatformOperatorByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        return _db.IdentityUsers.FirstOrDefaultAsync(
            u => u.EmailNormalized == normalized
                 && u.UserType == IdentityUserType.Platform
                 && u.PlatformRole == PlatformRole.PlatformOperator,
            ct);
    }

    public Task<IdentityUser?> GetPlatformUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        return _db.IdentityUsers.FirstOrDefaultAsync(
            u => u.EmailNormalized == normalized
                 && u.UserType == IdentityUserType.Platform
                 && u.PlatformRole != null,
            ct);
    }

    public Task<bool> AnyPrimaryPlatformOperatorAsync(CancellationToken ct = default)
        => _db.IdentityUsers.AnyAsync(
            u => u.UserType == IdentityUserType.Platform && u.PlatformRole == PlatformRole.PlatformOperator,
            ct);

    public Task<int> CountActivePlatformUsersAsync(CancellationToken ct = default)
        => _db.IdentityUsers.CountAsync(u => u.UserType == IdentityUserType.Platform && u.IsActive, ct);

    public Task<int> CountActiveCompanyUsersAsync(CancellationToken ct = default)
        => _db.IdentityUsers.CountAsync(u => u.UserType == IdentityUserType.Company && u.IsActive, ct);

    public Task<IdentityUser?> GetUserByIdAsync(Guid userId, CancellationToken ct = default)
        => _db.IdentityUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);

    public Task<IdentityUser?> GetUserByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        return _db.IdentityUsers.FirstOrDefaultAsync(u => u.EmailNormalized == normalized, ct);
    }

    public Task<bool> AnyUserWithEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = NormalizeEmail(email);
        return _db.IdentityUsers.AnyAsync(u => u.EmailNormalized == normalized, ct);
    }

    public Task<int> CountIdentityUsersAsync(CancellationToken ct = default)
        => _db.IdentityUsers.CountAsync(ct);

    public Task AddUserAsync(IdentityUser user, CancellationToken ct = default)
        => _db.IdentityUsers.AddAsync(user, ct).AsTask();

    public async Task<IReadOnlyList<CompanyUserMembership>> GetActiveCompanyUserMembershipsForUserSystemAsync(
        Guid identityUserId, CancellationToken ct = default)
        => await _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.CrossSubscriberSystem)
            .Where(m => m.IdentityUserId == identityUserId && m.IsActive)
            .ToListAsync(ct);

    public Task<CompanyUserMembership?> GetCompanyUserMembershipAsync(
        Guid companyId, Guid identityUserId, CancellationToken ct = default)
        => _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.CrossSubscriberSystem)
            .FirstOrDefaultAsync(m => m.CompanyId == companyId && m.IdentityUserId == identityUserId, ct);

    public Task AddCompanyUserMembershipAsync(CompanyUserMembership membership, CancellationToken ct = default)
        => _db.CompanyUserMemberships.AddAsync(membership, ct).AsTask();

    public async Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsByCompanyAsync(
        Guid companyId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            .Where(m => m.CompanyId == companyId);
        if (onlyActive) q = q.Where(m => m.IsActive);
        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CompanyUserMembership>> GetCompanyUserMembershipsBySubscriberAsync(
        Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q =
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId
            select m;

        if (onlyActive)
            q = q.Where(m => m.IsActive);

        return await q.OrderBy(m => m.IdentityUserId).ToListAsync(ct);
    }

    public Task<int> CountActiveCompanyUserMembershipsByCompanyAsync(Guid companyId, CancellationToken ct = default)
        => _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            .CountAsync(m => m.CompanyId == companyId && m.IsActive, ct);

    public Task<int> CountActiveCompanyUserMembershipsBySubscriberAsync(Guid subscriberId, CancellationToken ct = default)
        => (
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId && m.IsActive
            select m
        ).CountAsync(ct);

    public async Task<IReadOnlyList<AccessProfile>> GetProfilesBySubscriberAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
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

    public async Task<(int TotalUsers, int ActiveUsers)> CountDistinctIdentityUsersForSubscriberAsync(
        Guid subscriberId, CancellationToken ct = default)
    {
        var userIds = await (
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId
            select m.IdentityUserId
        ).Distinct().ToListAsync(ct);

        if (userIds.Count == 0)
            return (0, 0);

        var users = await _db.IdentityUsers
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.IsActive })
            .ToListAsync(ct);

        var activeMembershipUserIds = await (
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId && m.IsActive
            select m.IdentityUserId
        ).Distinct().ToListAsync(ct);

        var activeSet = activeMembershipUserIds.ToHashSet();
        var activeUsers = users.Count(u => u.IsActive && activeSet.Contains(u.Id));
        return (users.Count, activeUsers);
    }

    public async Task<IReadOnlyList<IdentityUser>> GetActiveIdentityUsersForSubscriberAsync(
        Guid subscriberId, CancellationToken ct = default)
    {
        var userIds = await (
            from m in _platform.Unfiltered(_db.CompanyUserMemberships, PlatformQueryReason.SubscriberScopedExplicit)
            join c in _db.Companies on m.CompanyId equals c.Id
            where c.SubscriberId == subscriberId && m.IsActive
            select m.IdentityUserId
        ).Distinct().ToListAsync(ct);

        return await _db.IdentityUsers
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    private static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();
}

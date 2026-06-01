using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Creates or updates the default access profiles (Facturador, Bodeguero, Contador) for a tenant.
/// Safe to call multiple times: new profiles are created, existing profiles have missing permissions
/// added (additive-only — never removes permissions that were customized by the admin).
/// Uses EF Core so it participates in the same DbContext transaction as tenant creation.
/// </summary>
public sealed class DefaultProfileSeeder : IDefaultProfileSeeder
{
    private readonly ErpDbContext _db;
    private readonly IPlatformQueryAccessor _platform;
    private readonly ILogger<DefaultProfileSeeder> _logger;

    public DefaultProfileSeeder(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        ILogger<DefaultProfileSeeder> logger)
    {
        _db       = db;
        _platform = platform;
        _logger   = logger;
    }

    public async Task SeedForSubscriberAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default)
    {
        var bundles = new[]
        {
            ("Facturador", "Billing operator — can create and void invoices.",   Permissions.FacilitadorProfile),
            ("Bodeguero",  "Warehouse operator — manages stock and transfers.",  Permissions.BodegueroProfile),
            ("Contador",   "Accountant — read and write access to accounting.", Permissions.ContadorProfile),
        };

        foreach (var (name, description, permKeys) in bundles)
        {
            var profile = await _platform
                .Unfiltered(_db.AccessProfiles, PlatformQueryReason.Seeding)
                .FirstOrDefaultAsync(p => p.SubscriberId == subscriberId && p.Name == name, ct);

            if (profile is null)
            {
                // New profile — create with all permissions
                profile = AccessProfile.Create(
                    subscriberId: subscriberId,
                    name:         name,
                    description:  description,
                    createdBy:    actorId);

                _db.AccessProfiles.Add(profile);
                await _db.SaveChangesAsync(ct);

                var newPermissions = permKeys.Select(key =>
                    AccessProfilePermission.Create(
                        subscriberId:  subscriberId,
                        profileId:     profile.Id,
                        permissionKey: key,
                        isAllowed:     true,
                        createdBy:     actorId));

                _db.AccessProfilePermissions.AddRange(newPermissions);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Default profile '{Name}' created for tenant {SubscriberId} ({Count} permissions).",
                    name, subscriberId, permKeys.Count);
            }
            else
            {
                // Existing profile — add any missing permissions (additive-only, never remove)
                var existing = await _platform
                    .Unfiltered(_db.AccessProfilePermissions, PlatformQueryReason.Seeding)
                    .Where(p => p.ProfileId == profile.Id)
                    .Select(p => p.PermissionKey)
                    .ToListAsync(ct);

                var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
                var missing = permKeys.Where(k => !existingSet.Contains(k)).ToList();

                if (missing.Count == 0)
                {
                    _logger.LogDebug(
                        "Default profile '{Name}' for tenant {SubscriberId} is up to date.",
                        name, subscriberId);
                    continue;
                }

                var addedPermissions = missing.Select(key =>
                    AccessProfilePermission.Create(
                        subscriberId:  subscriberId,
                        profileId:     profile.Id,
                        permissionKey: key,
                        isAllowed:     true,
                        createdBy:     actorId));

                _db.AccessProfilePermissions.AddRange(addedPermissions);
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Default profile '{Name}' for tenant {SubscriberId} updated: {Count} permission(s) added: {Keys}",
                    name, subscriberId, missing.Count, string.Join(", ", missing));
            }
        }
    }
}

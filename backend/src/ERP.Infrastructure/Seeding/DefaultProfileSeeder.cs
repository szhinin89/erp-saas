using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Kernel.Permissions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Creates or updates the default access profiles for a tenant. Currently seeds only "DataEntry"
/// (<see cref="DataEntryProfileBundle"/>) — additional bundles (e.g. Facturador, Bodeguero, Contador)
/// are a pending product decision, not yet implemented.
/// Safe to call multiple times: new profiles are created, existing profiles have missing permissions
/// added (additive-only — never removes permissions that were customized by the admin).
/// Uses EF Core so it participates in the same DbContext transaction as tenant creation.
/// </summary>
public sealed partial class DefaultProfileSeeder : IDefaultProfileSeeder
{
    private readonly ErpDbContext _db;
    private readonly ILogger<DefaultProfileSeeder> _logger;

    public DefaultProfileSeeder(
        ErpDbContext db,
        ILogger<DefaultProfileSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedForTenantAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var bundles = new[]
        {
            ("DataEntry", "Data entry operator — manages master data and items catalog.", DataEntryProfileBundle.Permissions),
        };

        foreach (var (name, description, permKeys) in bundles)
        {
            var profile = await _db.AccessProfiles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Name == name, cancellationToken);

            if (profile is null)
            {
                profile = AccessProfile.Create(
                    tenantId: tenantId,
                    name: name,
                    description: description,
                    createdBy: actorId);

                _db.AccessProfiles.Add(profile);
                await _db.SaveChangesAsync(cancellationToken);

                var newPermissions = permKeys.Select(key =>
                    AccessProfilePermission.Create(
                        tenantId: tenantId,
                        profileId: profile.Id,
                        permissionKey: key,
                        isAllowed: true,
                        createdBy: actorId));

                _db.AccessProfilePermissions.AddRange(newPermissions);
                await _db.SaveChangesAsync(cancellationToken);

                LogProfileCreated(name, tenantId, permKeys.Count);
            }
            else
            {
                var existing = await _db.AccessProfilePermissions.IgnoreQueryFilters()
                    .Where(p => p.ProfileId == profile.Id)
                    .Select(p => p.PermissionKey)
                    .ToListAsync(cancellationToken);

                var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
                var missing = permKeys.Where(k => !existingSet.Contains(k)).ToList();

                if (missing.Count == 0)
                {
                    LogProfileUpToDate(name, tenantId);
                    continue;
                }

                var addedPermissions = missing.Select(key =>
                    AccessProfilePermission.Create(
                        tenantId: tenantId,
                        profileId: profile.Id,
                        permissionKey: key,
                        isAllowed: true,
                        createdBy: actorId));

                _db.AccessProfilePermissions.AddRange(addedPermissions);
                await _db.SaveChangesAsync(cancellationToken);

                LogProfileUpdated(name, tenantId, missing.Count, _logger.IsEnabled(LogLevel.Information) ? string.Join(", ", missing) : string.Empty);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Default profile '{Name}' created for tenant {TenantId} ({Count} permissions).")]
    private partial void LogProfileCreated(string name, Guid tenantId, int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Default profile '{Name}' for tenant {TenantId} is up to date.")]
    private partial void LogProfileUpToDate(string name, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Default profile '{Name}' for tenant {TenantId} updated: {Count} permission(s) added: {Keys}")]
    private partial void LogProfileUpdated(string name, Guid tenantId, int count, string keys);
}

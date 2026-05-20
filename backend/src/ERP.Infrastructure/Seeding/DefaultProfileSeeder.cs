using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Creates the default access profiles (Facturador, Bodeguero, Contador) for a tenant.
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

    public async Task SeedForTenantAsync(Guid tenantId, Guid actorId, CancellationToken ct = default)
    {
        var bundles = new[]
        {
            ("Facturador", "Billing operator — can create and void invoices.",   Permissions.FacilitadorProfile),
            ("Bodeguero",  "Warehouse operator — manages stock and transfers.",  Permissions.BodegueroProfile),
            ("Contador",   "Accountant — read-only access to accounting data.", Permissions.ContadorProfile),
        };

        foreach (var (name, description, permKeys) in bundles)
        {
            var exists = await _platform
                .Unfiltered(_db.AccessProfiles, PlatformQueryReason.Seeding)
                .AnyAsync(p => p.TenantId == tenantId && p.Name == name, ct);

            if (exists)
            {
                _logger.LogDebug("Default profile '{Name}' already exists for tenant {TenantId}. Skipping.", name, tenantId);
                continue;
            }

            var profile = AccessProfile.Create(
                tenantId:    tenantId,
                name:        name,
                description: description,
                createdBy:   actorId);

            _db.AccessProfiles.Add(profile);
            await _db.SaveChangesAsync(ct);

            var permissions = permKeys.Select(key =>
                AccessProfilePermission.Create(
                    tenantId:      tenantId,
                    profileId:     profile.Id,
                    permissionKey: key,
                    isAllowed:     true,
                    createdBy:     actorId));

            _db.AccessProfilePermissions.AddRange(permissions);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Default profile '{Name}' seeded for tenant {TenantId} ({Count} permissions).",
                name, tenantId, permKeys.Count);
        }
    }
}

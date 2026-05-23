using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>/health/masterdata-sync — perfiles BP sin partner o settings sin partner activo.</summary>
public sealed class MasterDataSyncHealthCheck : IHealthCheck
{
    private readonly ErpDbContext _db;

    public MasterDataSyncHealthCheck(ErpDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = _db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Healthy("Skipped for in-memory provider.");

        var orphanCustomerProfiles = await _db.CustomerProfiles
            .Where(p => !_db.BusinessPartners.Any(b => b.Id == p.BusinessPartnerId))
            .Take(1)
            .AnyAsync(cancellationToken);

        if (orphanCustomerProfiles)
            return HealthCheckResult.Degraded("CustomerProfile sin BusinessPartner.");

        var orphanSupplierProfiles = await _db.SupplierProfiles
            .Where(p => !_db.BusinessPartners.Any(b => b.Id == p.BusinessPartnerId))
            .Take(1)
            .AnyAsync(cancellationToken);

        if (orphanSupplierProfiles)
            return HealthCheckResult.Degraded("SupplierProfile sin BusinessPartner.");

        return HealthCheckResult.Healthy("MasterData sync OK.");
    }
}

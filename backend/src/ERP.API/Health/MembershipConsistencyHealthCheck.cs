using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>/health/membership-consistency — memberships huérfanas o company/subscriber incoherentes.</summary>
public sealed class MembershipConsistencyHealthCheck : IHealthCheck
{
    private readonly ErpDbContext _db;

    public MembershipConsistencyHealthCheck(ErpDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var provider = _db.Database.ProviderName ?? string.Empty;
        if (provider.Contains("InMemory", StringComparison.OrdinalIgnoreCase))
            return HealthCheckResult.Healthy("Skipped for in-memory provider.");

        if (!await _db.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Database unreachable.");

        var orphanMemberships = await _db
            .CompanyUserMemberships.Where(m => !_db.Companies.Any(c => c.Id == m.CompanyId))
            .Take(1)
            .AnyAsync(cancellationToken);

        if (orphanMemberships)
            return HealthCheckResult.Degraded(
                "Memberships sin company/subscriber coherente detectadas."
            );

        return HealthCheckResult.Healthy("Membership consistency OK.");
    }
}

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

        // Company es ITenantScopedEntity: su query filter fail-closed devuelve 0 filas sin
        // contexto de tenant (el caso de este health check, que corre sin request autenticado).
        // IgnoreQueryFilters() es obligatorio aquí — sin él, todo membership se reporta como
        // huérfano aunque la company exista, igual que MasterDataReconciliation necesita
        // visibilidad cross-tenant para su chequeo de integridad de solo lectura.
        var orphanMemberships = await _db
            .CompanyUserMemberships.Where(m =>
                !_db.Companies.IgnoreQueryFilters().Any(c => c.Id == m.CompanyId)
            )
            .Take(1)
            .AnyAsync(cancellationToken);

        if (orphanMemberships)
            return HealthCheckResult.Degraded(
                "Memberships sin company/subscriber coherente detectadas."
            );

        return HealthCheckResult.Healthy("Membership consistency OK.");
    }
}

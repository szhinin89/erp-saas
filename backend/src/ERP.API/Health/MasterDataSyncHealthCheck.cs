using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ERP.API.Health;

/// <summary>
/// /health/masterdata-sync — verifica integridad básica del modelo BusinessPartner V2.
///
/// ELIMINADO (legacy): referencias a CustomerProfiles/SupplierProfiles
///   — esas tablas ya no existen en el modelo V2.
///
/// NUEVO: verifica que no existan BusinessPartnerRoles huérfanos (sin BP padre).
/// </summary>
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

        var orphanRoles = await _db.BusinessPartnerRoles
            .Where(r => !_db.BusinessPartners.Any(b => b.Id == r.BusinessPartnerId))
            .Take(1)
            .AnyAsync(cancellationToken);

        if (orphanRoles)
            return HealthCheckResult.Degraded("BusinessPartnerRole sin BusinessPartner padre.");

        return HealthCheckResult.Healthy("MasterData V2 integrity OK.");
    }
}

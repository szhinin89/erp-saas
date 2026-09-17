using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// BANK-CATALOG-01: backfill idempotente del catálogo mínimo de Bancos para tenants que ya
/// existían antes de este ticket — <see cref="Steps.BankCatalogBootstrapStep"/> solo siembra
/// empresas NUEVAS. Reutiliza <see cref="BankCatalogSeeder"/> — mismo criterio que
/// <see cref="MasterDataClassificationBackfillService"/>.
///
/// Invocación: <c>dotnet run -- backfill-bank-catalog</c> (ver Program.cs). No expone endpoint
/// HTTP — operación de despliegue de una sola vez, re-ejecutable sin riesgo.
/// </summary>
public sealed class BankCatalogBackfillService
{
    /// <summary>Actor de sistema para operaciones sin usuario autenticado (mismo criterio que MasterDataClassificationBackfillService).</summary>
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly ErpDbContext _db;
    private readonly BankCatalogSeeder _seeder;
    private readonly ILogger<BankCatalogBackfillService> _logger;

    public BankCatalogBackfillService(
        ErpDbContext db,
        BankCatalogSeeder seeder,
        ILogger<BankCatalogBackfillService> logger
    )
    {
        _db = db;
        _seeder = seeder;
        _logger = logger;
    }

    /// <summary>Itera todos los TenantId distintos en <c>Companies</c> y siembra el catálogo de Bancos para cada uno.</summary>
    public async Task<BankCatalogBackfillResult> RunAsync(CancellationToken ct = default)
    {
        var tenantIds = await _db
            .Companies.IgnoreQueryFilters()
            .Select(c => c.TenantId)
            .Distinct()
            .ToListAsync(ct);

        var totalAdded = 0;
        foreach (var tenantId in tenantIds)
        {
            var added = await _seeder.SeedAsync(tenantId, SystemActor, ct);
            totalAdded += added;
            _logger.LogInformation(
                "Backfill BankCatalog: tenant {TenantId} → {Added} banco(s) nuevo(s).",
                tenantId,
                added
            );
        }

        return new BankCatalogBackfillResult(tenantIds.Count, totalAdded);
    }
}

public sealed record BankCatalogBackfillResult(int TenantsProcessed, int RowsInserted);

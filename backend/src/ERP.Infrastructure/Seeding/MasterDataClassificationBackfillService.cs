using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Backfill idempotente de los 12 catálogos de clasificación de BusinessPartner
/// (CLASS-BP-CATALOGS-01) para empresas que ya existían antes de este bloque (p. ej. Sumak).
/// Reutiliza <see cref="MasterDataClassificationSeeder"/> — el mismo método que usa
/// <see cref="Steps.MasterDataClassificationBootstrapStep"/> para empresas nuevas — así que
/// re-ejecutar este servicio nunca duplica filas.
///
/// Invocación: <c>dotnet run -- backfill-master-data-classifications</c> (ver Program.cs) o desde
/// un test de integración contra la BD real/local. No expone endpoint HTTP — es una operación de
/// despliegue de una sola vez, no un flujo de usuario ni CRUD administrativo.
/// </summary>
public sealed class MasterDataClassificationBackfillService
{
    /// <summary>Actor de sistema para operaciones sin usuario autenticado (mismo criterio que <c>JournalFactory.SystemActor</c>).</summary>
    private static readonly Guid SystemActor = Guid.Empty;

    private readonly ErpDbContext _db;
    private readonly MasterDataClassificationSeeder _seeder;
    private readonly ILogger<MasterDataClassificationBackfillService> _logger;

    public MasterDataClassificationBackfillService(
        ErpDbContext db,
        MasterDataClassificationSeeder seeder,
        ILogger<MasterDataClassificationBackfillService> logger
    )
    {
        _db = db;
        _seeder = seeder;
        _logger = logger;
    }

    /// <summary>
    /// Itera todas las (TenantId, CompanyId) existentes en <c>Companies</c> y siembra los 12
    /// catálogos para cada una. Devuelve un resumen (empresas procesadas, filas insertadas).
    /// </summary>
    public async Task<MasterDataClassificationBackfillResult> RunAsync(
        CancellationToken ct = default
    )
    {
        var pairs = await _db
            .Companies.IgnoreQueryFilters()
            .Select(c => new { c.TenantId, CompanyId = c.Id })
            .Distinct()
            .ToListAsync(ct);

        var totalAdded = 0;
        foreach (var pair in pairs)
        {
            var added = await _seeder.SeedAsync(pair.TenantId, pair.CompanyId, SystemActor, ct);
            totalAdded += added;
            _logger.LogInformation(
                "Backfill MasterDataClassifications: tenant {TenantId}, empresa {CompanyId} → {Added} fila(s) nueva(s).",
                pair.TenantId,
                pair.CompanyId,
                added
            );
        }

        return new MasterDataClassificationBackfillResult(pairs.Count, totalAdded);
    }
}

public sealed record MasterDataClassificationBackfillResult(
    int CompaniesProcessed,
    int RowsInserted
);

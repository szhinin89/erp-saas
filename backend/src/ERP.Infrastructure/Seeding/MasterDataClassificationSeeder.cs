using ERP.Domain.Common;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding;

/// <summary>
/// Lógica idempotente única de siembra de los 12 catálogos de clasificación de BusinessPartner
/// (CLASS-BP-CATALOGS-01). Reutilizada por dos consumidores distintos que NUNCA deben duplicar
/// esta lógica (ver plan CLASS-BP-CATALOGS-01):
///   1. <see cref="Steps.MasterDataClassificationBootstrapStep"/> — corre en la creación de cada
///      empresa nueva (bootstrap normal).
///   2. <c>MasterDataClassificationBackfillService</c> — corre una vez contra todas las empresas
///      ya existentes al desplegar este bloque (Sumak incluida), y puede re-ejecutarse sin
///      duplicar filas.
///
/// Los códigos sembrados son exactamente los que hoy validan los HashSet fijos
/// <c>CustomerRoleConfig.ValidX</c> / <c>SupplierClassificationConfig.ValidX</c> (y, para
/// CustomerClassification, el array <c>CUSTOMER_CLASSIFICATIONS</c> del frontend) — no se pierde
/// ni se renombra ningún valor ya usado en producción.
/// </summary>
public sealed class MasterDataClassificationSeeder
{
    private readonly ErpDbContext _db;
    private readonly ILogger<MasterDataClassificationSeeder> _logger;

    public MasterDataClassificationSeeder(
        ErpDbContext db,
        ILogger<MasterDataClassificationSeeder> logger
    )
    {
        _db = db;
        _logger = logger;
    }

    // ── Seed lists (Code, Name, SortOrder) — origen: tabla del plan CLASS-BP-CATALOGS-01 ──────

    private static readonly (string Code, string Name, int Sort)[] CustomerCategorySeed =
    [
        ("Retail", "Minorista", 1),
        ("Wholesale", "Mayorista", 2),
        ("Corporate", "Corporativo", 3),
        ("Government", "Gobierno", 4),
        ("VIP", "VIP", 5),
        ("Other", "Otro", 6),
    ];

    private static readonly (string Code, string Name, int Sort)[] CustomerSegmentSeed =
    [
        ("Individual", "Individual", 1),
        ("SMB", "Pequeña y Mediana Empresa", 2),
        ("MidMarket", "Mercado Medio", 3),
        ("Enterprise", "Empresarial", 4),
        ("StartUp", "Startup", 5),
    ];

    private static readonly (string Code, string Name, int Sort)[] CreditRatingSeed =
    [
        ("AAA", "AAA (Excelente)", 1),
        ("AA", "AA (Muy bueno)", 2),
        ("A", "A (Bueno)", 3),
        ("BBB", "BBB (Aceptable)", 4),
        ("BB", "BB (Regular)", 5),
        ("B", "B (Bajo)", 6),
        ("C", "C (Riesgo alto)", 7),
        ("D", "D (Riesgo crítico)", 8),
        ("NR", "NR (No calificado)", 9),
    ];

    private static readonly (string Code, string Name, int Sort)[] LoyaltyTierSeed =
    [
        ("None", "Ninguno", 1),
        ("Bronze", "Bronce", 2),
        ("Silver", "Plata", 3),
        ("Gold", "Oro", 4),
        ("Platinum", "Platino", 5),
    ];

    private static readonly (string Code, string Name, int Sort)[] InvoiceFormatSeed =
    [
        ("PDF", "PDF", 1),
        ("XML", "XML", 2),
        ("EMAIL", "Correo electrónico", 3),
        ("PORTAL", "Portal web", 4),
        ("PAPER", "Papel físico", 5),
    ];

    private static readonly (string Code, string Name, int Sort)[] CustomerClassificationSeed =
    [
        ("Nacional", "Nacional", 1),
        ("Exportador", "Exportador", 2),
        ("Importador", "Importador", 3),
        ("Exento", "Exento", 4),
        ("Especial", "Especial", 5),
    ];

    private static readonly (string Code, string Name, int Sort)[] SupplierCategorySeed =
    [
        ("Manufacturer", "Fabricante", 1),
        ("Distributor", "Distribuidor", 2),
        ("ServiceProvider", "Proveedor de servicios", 3),
        ("Agent", "Agente", 4),
        ("Retailer", "Minorista", 5),
        ("Other", "Otro", 6),
    ];

    private static readonly (string Code, string Name, int Sort)[] SupplierTypeSeed =
    [
        ("National", "Nacional", 1),
        ("International", "Internacional", 2),
        ("Both", "Ambos", 3),
    ];

    private static readonly (string Code, string Name, int Sort)[] SupplierRiskSeed =
    [
        ("Low", "Bajo", 1),
        ("Medium", "Medio", 2),
        ("High", "Alto", 3),
        ("Critical", "Crítico", 4),
    ];

    private static readonly (string Code, string Name, int Sort)[] SupplierRatingSeed =
    [
        ("AAA", "AAA (Excelente)", 1),
        ("AA", "AA (Muy bueno)", 2),
        ("A", "A (Bueno)", 3),
        ("BBB", "BBB (Aceptable)", 4),
        ("BB", "BB (Regular)", 5),
        ("B", "B (Bajo)", 6),
        ("C", "C (Riesgo alto)", 7),
        ("D", "D (Riesgo crítico)", 8),
        ("NR", "NR (No calificado)", 9),
    ];

    private static readonly (string Code, string Name, int Sort)[] PrimaryGoodTypeSeed =
    [
        ("Goods", "Bienes", 1),
        ("Services", "Servicios", 2),
        ("Both", "Ambos", 3),
        ("Digital", "Digital", 4),
    ];

    private static readonly (string Code, string Name, int Sort)[] SupplierSegmentSeed =
    [
        ("Strategic", "Estratégico", 1),
        ("Preferred", "Preferido", 2),
        ("Approved", "Aprobado", 3),
        ("Transactional", "Transaccional", 4),
    ];

    /// <summary>
    /// Siembra los 12 catálogos para (tenantId, companyId) — idempotente: solo inserta los
    /// códigos que aún no existen para esa empresa. Devuelve el total de filas insertadas
    /// (0 en una segunda corrida, salvo que se hayan agregado catálogos nuevos).
    /// </summary>
    public async Task<int> SeedAsync(
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        CancellationToken ct = default
    )
    {
        var added = 0;

        added += await SeedOneAsync(
            _db.CustomerCategories,
            tenantId,
            companyId,
            actorId,
            CustomerCategorySeed,
            CustomerCategory.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.CustomerSegments,
            tenantId,
            companyId,
            actorId,
            CustomerSegmentSeed,
            CustomerSegment.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.CustomerCreditRatings,
            tenantId,
            companyId,
            actorId,
            CreditRatingSeed,
            CustomerCreditRating.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.LoyaltyTiers,
            tenantId,
            companyId,
            actorId,
            LoyaltyTierSeed,
            LoyaltyTier.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.CustomerInvoiceFormats,
            tenantId,
            companyId,
            actorId,
            InvoiceFormatSeed,
            CustomerInvoiceFormat.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.CustomerClassifications,
            tenantId,
            companyId,
            actorId,
            CustomerClassificationSeed,
            CustomerClassification.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.SupplierCategories,
            tenantId,
            companyId,
            actorId,
            SupplierCategorySeed,
            SupplierCategory.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.SupplierTypes,
            tenantId,
            companyId,
            actorId,
            SupplierTypeSeed,
            SupplierType.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.SupplierRisks,
            tenantId,
            companyId,
            actorId,
            SupplierRiskSeed,
            SupplierRisk.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.SupplierRatings,
            tenantId,
            companyId,
            actorId,
            SupplierRatingSeed,
            SupplierRating.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.PrimaryGoodTypes,
            tenantId,
            companyId,
            actorId,
            PrimaryGoodTypeSeed,
            PrimaryGoodType.CreateSystemSeeded,
            ct
        );
        added += await SeedOneAsync(
            _db.SupplierSegments,
            tenantId,
            companyId,
            actorId,
            SupplierSegmentSeed,
            SupplierSegment.CreateSystemSeeded,
            ct
        );

        if (added > 0)
            _logger.LogInformation(
                "MasterDataClassifications: {Added} fila(s) sembrada(s) para tenant {TenantId}, empresa {CompanyId}.",
                added,
                tenantId,
                companyId
            );

        return added;
    }

    private async Task<int> SeedOneAsync<TEntity>(
        DbSet<TEntity> set,
        Guid tenantId,
        Guid companyId,
        Guid actorId,
        (string Code, string Name, int Sort)[] seed,
        Func<Guid, Guid, string, string, int, Guid, TEntity> factory,
        CancellationToken ct
    )
        where TEntity : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity, IClassificationCatalogEntity
    {
        var existingCodes = await set.IgnoreQueryFilters()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId)
            .Select(e => e.Code)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (code, name, sort) in seed)
        {
            if (existingSet.Contains(code))
                continue;

            set.Add(factory(tenantId, companyId, code, name, sort, actorId));
            added++;
        }

        if (added > 0)
            await _db.SaveChangesAsync(ct);

        return added;
    }
}

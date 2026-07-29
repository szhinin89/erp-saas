using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Enums;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Seeding.Steps;

/// <summary>
/// Configuración mínima del dominio Items: tipos de ítem por defecto (Tipo A) y el registro
/// protegido "No aplica" de los catálogos operativos Tipo C (Marca, Categoría de ítem) — ver
/// política de Bootstrap en <c>CLAUDE.md</c>. Todos los pasos son idempotentes.
/// </summary>
public sealed partial class InventoryBootstrapStep : ICompanyBootstrapStep
{
    public int Order => CompanyBootstrapStepOrder.Inventory;

    private const string NoAplicaName = "No aplica";

    private static readonly (string Code, string Name, int Sort)[] DefaultItemTypes =
    [
        ("Physical", "Físico",   1),
        ("Service",  "Servicio", 2),
        ("Digital",  "Digital",  3),
        ("Kit",      "Kit",      4),
        ("Bundle",   "Bundle",   5),
    ];

    private readonly ErpDbContext _db;
    private readonly ILogger<InventoryBootstrapStep> _logger;

    public InventoryBootstrapStep(ErpDbContext db, ILogger<InventoryBootstrapStep> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ExecuteAsync(CompanyBootstrapContext context, CancellationToken cancellationToken = default)
    {
        var (tenantId, _, actorId) = context;

        await SeedDefaultItemTypesAsync(tenantId, actorId, cancellationToken);
        await SeedNoAplicaBrandAsync(tenantId, actorId, cancellationToken);
        await SeedNoAplicaCategoryAsync(tenantId, actorId, cancellationToken);
    }

    private async Task SeedDefaultItemTypesAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        var existingCodes = await _db.ItemTypes.IgnoreQueryFilters()
            .Where(it => it.TenantId == tenantId)
            .Select(it => it.Code)
            .ToListAsync(cancellationToken);

        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var (code, name, sort) in DefaultItemTypes)
        {
            if (existingSet.Contains(code))
            {
                LogItemTypeSkipped(code, tenantId);
                continue;
            }

            var it = ItemTypeDefinition.CreateSystemSeeded(tenantId, code, name, sort, actorId);
            _db.ItemTypes.Add(it);
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            LogItemTypesSeeded(added, tenantId);
        }
    }

    private async Task SeedNoAplicaBrandAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.Brands.IgnoreQueryFilters()
            .AnyAsync(b => b.TenantId == tenantId && b.Code == Brand.NoAplicaCode, ct);

        if (exists)
        {
            LogNoAplicaBrandSkipped(tenantId);
            return;
        }

        var brand = Brand.CreateSystemSeeded(tenantId, Brand.NoAplicaCode, NoAplicaName, actorId);
        _db.Brands.Add(brand);
        await _db.SaveChangesAsync(ct);

        LogNoAplicaBrandSeeded(tenantId, brand.Id);
    }

    private async Task SeedNoAplicaCategoryAsync(Guid tenantId, Guid actorId, CancellationToken ct)
    {
        var exists = await _db.ItemCategoryNodes.IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && c.Code == ItemCategoryNode.NoAplicaCode, ct);

        if (exists)
        {
            LogNoAplicaCategorySkipped(tenantId);
            return;
        }

        var category = ItemCategoryNode.CreateSystemSeeded(
            tenantId: tenantId,
            code: ItemCategoryNode.NoAplicaCode,
            name: NoAplicaName,
            level: CategoryNodeLevel.Family,
            createdBy: actorId);
        category.SetPath($"/{category.Id}");

        _db.ItemCategoryNodes.Add(category);
        await _db.SaveChangesAsync(ct);

        LogNoAplicaCategorySeeded(tenantId, category.Id);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "ItemType {Code} already exists for tenant {TenantId}. Skipping.")]
    private partial void LogItemTypeSkipped(string code, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{Count} default item type(s) seeded for tenant {TenantId}.")]
    private partial void LogItemTypesSeeded(int count, Guid tenantId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Brand 'No aplica' already exists for tenant {TenantId}. Skipping.")]
    private partial void LogNoAplicaBrandSkipped(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Brand 'No aplica' seeded for tenant {TenantId} (id={BrandId}).")]
    private partial void LogNoAplicaBrandSeeded(Guid tenantId, Guid brandId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ItemCategoryNode 'No aplica' already exists for tenant {TenantId}. Skipping.")]
    private partial void LogNoAplicaCategorySkipped(Guid tenantId);

    [LoggerMessage(Level = LogLevel.Information, Message = "ItemCategoryNode 'No aplica' seeded for tenant {TenantId} (id={CategoryId}).")]
    private partial void LogNoAplicaCategorySeeded(Guid tenantId, Guid categoryId);
}

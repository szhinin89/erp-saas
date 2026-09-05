using ERP.Domain.Common;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.MasterData;

/// <summary>
/// Base genérica compartida por los repositorios de los 12 catálogos de clasificación de
/// BusinessPartner (CLASS-BP-CATALOGS-01). Cada catálogo mantiene su propia interfaz concreta en
/// <c>ERP.Domain.MasterData.Interfaces</c> (para poder mockear en tests de handler); la
/// implementación reutiliza esta base para no repetir la misma consulta 12 veces.
///
/// Usa <c>IgnoreQueryFilters()</c> + filtro explícito de tenant/company: estos repos se consumen
/// tanto desde handlers request-scoped (vía <c>ICurrentTenant</c>/<c>ICurrentCompany</c>) como
/// desde el bootstrap step y el backfill de empresas existentes, que iteran múltiples
/// (TenantId, CompanyId) sin contexto HTTP ambiente — igual que <c>InventoryBootstrapStep</c>.
/// Fail-closed: sin TenantId/CompanyId válidos, el filtro explícito no matchea ninguna fila.
/// </summary>
public abstract class ClassificationCatalogRepositoryBase<TEntity>
    where TEntity : MasterEntity, ITenantScopedEntity, ICompanyOperationalEntity, IClassificationCatalogEntity
{
    protected readonly ErpDbContext Db;

    protected ClassificationCatalogRepositoryBase(ErpDbContext db) => Db = db;

    protected IQueryable<TEntity> Scoped(Guid tenantId, Guid companyId) =>
        Db.Set<TEntity>()
            .AsPlatformQuery()
            .Where(e => e.TenantId == tenantId && e.CompanyId == companyId);

    public async Task<IReadOnlyList<TEntity>> GetActiveAsync(
        Guid tenantId,
        Guid companyId,
        CancellationToken ct = default
    ) =>
        await Scoped(tenantId, companyId)
            .Where(e => e.IsActive)
            .OrderBy(e => e.SortOrder)
            .ThenBy(e => e.Name)
            .ToListAsync(ct);

    public async Task<bool> CodeExistsAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    ) => await Scoped(tenantId, companyId).AnyAsync(e => e.Code == code, ct);

    public async Task<bool> CodeExistsActiveAsync(
        Guid tenantId,
        Guid companyId,
        string code,
        CancellationToken ct = default
    ) => await Scoped(tenantId, companyId).AnyAsync(e => e.Code == code && e.IsActive, ct);

    public async Task AddAsync(TEntity entity, CancellationToken ct = default) =>
        await Db.Set<TEntity>().AddAsync(entity, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await Db.SaveChangesAsync(ct);
}

using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Interfaces;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Repositories;

/// <summary>
/// Repositorio del Aggregate Root SupplierRetentionDefault —
/// RETENTIONS-SUPPLIER-DEFAULTS-DYNAMIC-01.
/// Scope: ICompanyScopedEntity + ITenantScopedEntity.
/// El global query filter aplica WHERE tenant_id = @tenant AND company_id = @company en cada
/// query. Filtro FAIL-CLOSED en ambas dimensiones — sin company context → 0 filas.
/// </summary>
public sealed class SupplierRetentionDefaultRepository : ISupplierRetentionDefaultRepository
{
    private readonly ErpDbContext _db;

    public SupplierRetentionDefaultRepository(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyList<SupplierRetentionDefault>> GetByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SupplierRetentionDefaults.Where(x => x.BusinessPartnerId == businessPartnerId)
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SupplierRetentionDefault>> GetActiveByBusinessPartnerAsync(
        Guid businessPartnerId,
        CancellationToken cancellationToken = default
    ) =>
        await _db
            .SupplierRetentionDefaults.Where(x =>
                x.BusinessPartnerId == businessPartnerId && x.IsActive
            )
            .OrderBy(x => x.DisplayOrder)
            .ToListAsync(cancellationToken);

    public Task<SupplierRetentionDefault?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => _db.SupplierRetentionDefaults.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(
        SupplierRetentionDefault entry,
        CancellationToken cancellationToken = default
    ) => await _db.SupplierRetentionDefaults.AddAsync(entry, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}

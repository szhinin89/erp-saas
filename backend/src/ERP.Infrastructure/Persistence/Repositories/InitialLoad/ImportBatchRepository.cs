using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.InitialLoad;

public sealed class ImportBatchRepository : IImportBatchRepository
{
    private readonly ErpDbContext _context;

    public ImportBatchRepository(ErpDbContext context) => _context = context;

    // El global query filter de EF ya aplica tenant+company; el filtro explícito es defensa en
    // profundidad para llamadas sin contexto HTTP (mismo patrón que ItemRepository.Scoped).
    private IQueryable<ImportBatch> Scoped(Guid tenantId, Guid companyId) =>
        _context.ImportBatches.Where(x => x.TenantId == tenantId && x.CompanyId == companyId);

    public async Task<ImportBatch?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    ) =>
        await Scoped(tenantId, companyId)
            .Include(x => x.Files)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<ImportBatch> Batches, int TotalCount)> GetPageAsync(
        Guid tenantId,
        Guid companyId,
        ImportType? importType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
        var query = Scoped(tenantId, companyId);
        if (importType.HasValue)
            query = query.Where(x => x.ImportType == importType.Value);

        var total = await query.CountAsync(cancellationToken);
        var batches = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (batches, total);
    }

    public async Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default) =>
        await _context.ImportBatches.AddAsync(batch, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}

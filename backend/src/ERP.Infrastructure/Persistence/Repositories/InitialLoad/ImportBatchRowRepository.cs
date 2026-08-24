using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.InitialLoad;

public sealed class ImportBatchRowRepository : IImportBatchRowRepository
{
    private readonly ErpDbContext _context;

    public ImportBatchRowRepository(ErpDbContext context) => _context = context;

    private IQueryable<ImportBatchRow> Scoped(Guid importBatchId, Guid tenantId, Guid companyId) =>
        _context.ImportBatchRows.Where(x =>
            x.ImportBatchId == importBatchId && x.TenantId == tenantId && x.CompanyId == companyId
        );

    public async Task AddRangeAsync(
        IEnumerable<ImportBatchRow> rows,
        CancellationToken cancellationToken = default
    ) => await _context.ImportBatchRows.AddRangeAsync(rows, cancellationToken);

    public async Task<(IReadOnlyList<ImportBatchRow> Rows, int TotalCount)> GetPageAsync(
        Guid importBatchId,
        Guid tenantId,
        Guid companyId,
        int pageNumber,
        int pageSize,
        bool? onlyWithBlockingIssue,
        CancellationToken cancellationToken = default
    )
    {
        var query = Scoped(importBatchId, tenantId, companyId);
        if (onlyWithBlockingIssue.HasValue)
            query = query.Where(x => x.HasBlockingIssue == onlyWithBlockingIssue.Value);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.RowNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (rows, total);
    }

    public async Task<IReadOnlyList<ImportBatchRow>> GetValidRowsPageAsync(
        Guid importBatchId,
        Guid tenantId,
        Guid companyId,
        int pageSize,
        CancellationToken cancellationToken = default
    ) =>
        await Scoped(importBatchId, tenantId, companyId)
            .Where(x => !x.HasBlockingIssue && !x.IsImported)
            .OrderBy(x => x.RowNumber)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}

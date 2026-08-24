using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.InitialLoad;

public sealed class ImportBatchIssueRepository : IImportBatchIssueRepository
{
    private readonly ErpDbContext _context;

    public ImportBatchIssueRepository(ErpDbContext context) => _context = context;

    public async Task AddRangeAsync(
        IEnumerable<ImportBatchIssue> issues,
        CancellationToken cancellationToken = default
    ) => await _context.ImportBatchIssues.AddRangeAsync(issues, cancellationToken);

    public async Task AddAsync(
        ImportBatchIssue issue,
        CancellationToken cancellationToken = default
    ) => await _context.ImportBatchIssues.AddAsync(issue, cancellationToken);

    public async Task<IReadOnlyList<ImportBatchIssue>> GetByRowIdsAsync(
        IReadOnlyCollection<Guid> importBatchRowIds,
        CancellationToken cancellationToken = default
    ) =>
        importBatchRowIds.Count == 0
            ? []
            : await _context
                .ImportBatchIssues.Where(x => importBatchRowIds.Contains(x.ImportBatchRowId))
                .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ImportBatchIssue>> GetByBatchAsync(
        Guid importBatchId,
        Guid tenantId,
        Guid companyId,
        ImportSeverity? severity,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.ImportBatchIssues.Where(x =>
            x.ImportBatchId == importBatchId && x.TenantId == tenantId && x.CompanyId == companyId
        );
        if (severity.HasValue)
            query = query.Where(x => x.Severity == severity.Value);

        return await query.OrderBy(x => x.RowNumber).ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}

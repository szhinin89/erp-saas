using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

public interface IImportBatchIssueRepository
{
    Task AddRangeAsync(
        IEnumerable<ImportBatchIssue> issues,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(ImportBatchIssue issue, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportBatchIssue>> GetByRowIdsAsync(
        IReadOnlyCollection<Guid> importBatchRowIds,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<ImportBatchIssue>> GetByBatchAsync(
        Guid importBatchId,
        Guid tenantId,
        Guid companyId,
        ImportSeverity? severity,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

public interface IImportBatchRepository
{
    Task<ImportBatch?> GetByIdAsync(
        Guid id,
        Guid tenantId,
        Guid companyId,
        CancellationToken cancellationToken = default
    );

    Task<(IReadOnlyList<ImportBatch> Batches, int TotalCount)> GetPageAsync(
        Guid tenantId,
        Guid companyId,
        ImportType? importType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

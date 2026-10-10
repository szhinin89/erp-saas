using ERP.Domain.Modules.InitialLoad.Entities;

namespace ERP.Domain.Modules.InitialLoad.Interfaces;

/// <summary>IL-7A — estado contable de la apertura por lote (uno por empresa + lote).</summary>
public interface IOpeningBalancePostingRepository
{
    Task<OpeningBalancePosting?> FindByBatchAsync(
        Guid tenantId,
        Guid companyId,
        Guid importBatchId,
        CancellationToken cancellationToken = default
    );

    Task AddAsync(OpeningBalancePosting posting, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

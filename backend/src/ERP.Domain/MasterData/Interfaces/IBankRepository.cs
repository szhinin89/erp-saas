using ERP.Domain.MasterData.Entities;

namespace ERP.Domain.MasterData.Interfaces;

public interface IBankRepository
{
    Task<IReadOnlyList<Bank>> ListAsync(
        Guid tenantId,
        bool onlyActive = false,
        string? search = null,
        CancellationToken cancellationToken = default
    );
    Task<Bank?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<bool> ExistsByCodeAsync(
        Guid tenantId,
        string countryCode,
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default
    );
    Task AddAsync(Bank entity, CancellationToken cancellationToken = default);
    void Update(Bank entity);
}

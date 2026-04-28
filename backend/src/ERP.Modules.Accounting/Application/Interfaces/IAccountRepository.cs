using Modules.Accounting.Domain.Entities;

namespace Modules.Accounting.Application.Interfaces;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<Account?> GetByCodeAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Account>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

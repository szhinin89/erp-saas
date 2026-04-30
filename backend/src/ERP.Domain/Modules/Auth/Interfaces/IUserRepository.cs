using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<User?> GetByIdSystemAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<User?> GetByEmailSystemAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<User?> GetSingleSuperAdminByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnySuperAdminAsync(CancellationToken ct = default);
    Task<int> CountAllSystemAsync(CancellationToken ct = default);
    Task<int> CountActiveSystemAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(string email, Guid tenantId, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

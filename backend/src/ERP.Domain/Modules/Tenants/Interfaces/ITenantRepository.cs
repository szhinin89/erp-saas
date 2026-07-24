using ERP.Domain.Tenants.Entities;

namespace ERP.Domain.Tenants.Interfaces;

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

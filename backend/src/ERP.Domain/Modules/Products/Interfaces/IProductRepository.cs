using ERP.Domain.Products.Entities;

namespace ERP.Domain.Products.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

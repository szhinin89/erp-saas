using ERP.Domain.Products.Entities;

namespace ERP.Domain.Products.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<Product?> GetByIdWithDetailsAsync(Guid id, Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetReportAsync(Guid tenantId, ProductReportFilter filter, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetReportPageAsync(
        Guid tenantId,
        ProductReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

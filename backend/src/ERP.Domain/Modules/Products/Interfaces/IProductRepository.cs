using ERP.Domain.Products.Entities;

namespace ERP.Domain.Products.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetAllBySubscriberAsync(Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetReportAsync(Guid subscriberId, ProductReportFilter filter, CancellationToken ct = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetReportPageAsync(
        Guid subscriberId,
        ProductReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task UpdateAsync(Product product, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

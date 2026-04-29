using Microsoft.EntityFrameworkCore;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ErpDbContext _context;

    public ProductRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<Product>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _context.Products
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.SaleCode)
            .ToListAsync(ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await _context.Products.AddAsync(product, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
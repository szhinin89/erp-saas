using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class ItemCatalogRepository : IItemCatalogRepository
{
    private readonly ErpDbContext _context;

    public ItemCatalogRepository(ErpDbContext context) => _context = context;

    public async Task<Brand?> GetBrandByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    ) => await _context.Brands.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Brand>> GetBrandsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) =>
        await _context
            .Brands.Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

    public async Task<bool> BrandCodeExistsAsync(
        string code,
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.Brands.AnyAsync(
            x => x.TenantId == tenantId && x.Code == normalized,
            cancellationToken
        );
    }

    public async Task AddBrandAsync(Brand brand, CancellationToken cancellationToken = default) =>
        await _context.Brands.AddAsync(brand, cancellationToken);

    public async Task<bool> BarcodeTypeExistsAndActiveAsync(
        string code,
        CancellationToken cancellationToken = default
    )
    {
        var trimmed = code.Trim();
        return await _context.BarcodeTypes.AnyAsync(
            x => x.Code == trimmed && x.IsActive,
            cancellationToken
        );
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        await _context.SaveChangesAsync(cancellationToken);
}

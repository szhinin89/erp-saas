using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ErpDbContext _context;
    private readonly ICurrentCompany _company;

    public ProductRepository(ErpDbContext context, ICurrentCompany company)
    {
        _context = context;
        _company = company;
    }

    private IQueryable<Product> Scoped(Guid subscriberId)
        => _context.Products.ForOperationalScope(subscriberId, _company);

    public async Task<Product?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await Scoped(subscriberId).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product?> GetByIdWithLinesAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await Scoped(subscriberId)
            .Include(p => p.Barcodes)
            .Include(p => p.SupplierCodes)
            .Include(p => p.UnitConversions)
            .Include(p => p.Colors)
            .Include(p => p.Sizes)
            .Include(p => p.Dimensions)
            .Include(p => p.Images)
            .Include(p => p.Features)
            .Include(p => p.TariffDetails)
            .Include(p => p.Substitutes)
            .Include(p => p.CustomFields)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetAllBySubscriberAsync(Guid subscriberId, CancellationToken ct = default)
        => await Scoped(subscriberId)
            .Where(p => p.IsActive)
            .OrderBy(p => p.SaleCode)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> GetReportAsync(Guid subscriberId, ProductReportFilter filter, CancellationToken ct = default)
    {
        var query = Scoped(subscriberId);

        if (filter.IsActive is not null)
            query = query.Where(p => p.IsActive == filter.IsActive);

        if (filter.IsForSale is not null)
            query = query.Where(p => p.IsForSale == filter.IsForSale);

        if (filter.IsFavorite is not null)
            query = query.Where(p => p.IsFavorite == filter.IsFavorite);

        if (filter.IsEcommerceActive is not null)
            query = query.Where(p => p.IsEcommerceActive == filter.IsEcommerceActive);

        if (filter.IsService is not null)
            query = query.Where(p => p.IsService == filter.IsService);

        if (filter.LineId is not null)
            query = query.Where(p => p.LineId == filter.LineId);

        if (filter.CategoryId is not null)
            query = query.Where(p => p.CategoryId == filter.CategoryId);

        if (filter.SubcategoryId is not null)
            query = query.Where(p => p.SubcategoryId == filter.SubcategoryId);

        if (filter.BrandId is not null)
            query = query.Where(p => p.BrandId == filter.BrandId);

        if (filter.ProductTypeId is not null)
            query = query.Where(p => p.ProductTypeId == filter.ProductTypeId);

        if (!string.IsNullOrWhiteSpace(filter.SaleCode))
            query = query.Where(p => p.SaleCode == filter.SaleCode);

        if (!string.IsNullOrWhiteSpace(filter.PurchaseCode))
            query = query.Where(p => p.PurchaseCode == filter.PurchaseCode);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.SaleCode.ToLower().Contains(s) ||
                (p.PurchaseCode != null && p.PurchaseCode.ToLower().Contains(s)) ||
                p.ShortName.ToLower().Contains(s) ||
                p.Description.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var code = filter.Barcode.Trim();
            query = query.Include(p => p.Barcodes)
                         .Where(p => p.Barcodes.Any(b => b.Code == code));
        }

        return await query
            .OrderBy(p => p.SaleCode)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetReportPageAsync(
        Guid subscriberId,
        ProductReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var query = _context.Products.AsQueryable()
            .Where(p => p.SubscriberId == subscriberId);

        if (filter.IsActive is not null)
            query = query.Where(p => p.IsActive == filter.IsActive);

        if (filter.IsForSale is not null)
            query = query.Where(p => p.IsForSale == filter.IsForSale);

        if (filter.IsFavorite is not null)
            query = query.Where(p => p.IsFavorite == filter.IsFavorite);

        if (filter.IsEcommerceActive is not null)
            query = query.Where(p => p.IsEcommerceActive == filter.IsEcommerceActive);

        if (filter.IsService is not null)
            query = query.Where(p => p.IsService == filter.IsService);

        if (filter.LineId is not null)
            query = query.Where(p => p.LineId == filter.LineId);

        if (filter.CategoryId is not null)
            query = query.Where(p => p.CategoryId == filter.CategoryId);

        if (filter.SubcategoryId is not null)
            query = query.Where(p => p.SubcategoryId == filter.SubcategoryId);

        if (filter.BrandId is not null)
            query = query.Where(p => p.BrandId == filter.BrandId);

        if (filter.ProductTypeId is not null)
            query = query.Where(p => p.ProductTypeId == filter.ProductTypeId);

        if (!string.IsNullOrWhiteSpace(filter.SaleCode))
            query = query.Where(p => p.SaleCode == filter.SaleCode);

        if (!string.IsNullOrWhiteSpace(filter.PurchaseCode))
            query = query.Where(p => p.PurchaseCode == filter.PurchaseCode);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.SaleCode.ToLower().Contains(s) ||
                (p.PurchaseCode != null && p.PurchaseCode.ToLower().Contains(s)) ||
                p.ShortName.ToLower().Contains(s) ||
                p.Description.ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var code = filter.Barcode.Trim();
            query = query.Include(p => p.Barcodes)
                         .Where(p => p.Barcodes.Any(b => b.Code == code && b.IsActive));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.SaleCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(Product product, CancellationToken ct = default)
        => await _context.Products.AddAsync(product, ct);

    public Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _context.Products.Update(product);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}
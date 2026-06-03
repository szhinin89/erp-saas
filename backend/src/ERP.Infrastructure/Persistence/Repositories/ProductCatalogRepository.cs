using ERP.Application.Common;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Products.Entities;
using ERP.Domain.Products.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public class ProductCatalogRepository : IProductCatalogRepository
{
    private readonly ErpDbContext _context;
    private readonly IPlatformQueryAccessor _platform;

    public ProductCatalogRepository(ErpDbContext context, IPlatformQueryAccessor platform)
    {
        _context = context;
        _platform = platform;
    }

    public Task AddBrandAsync(Brand brand, CancellationToken ct = default)
        => _context.Brands.AddAsync(brand, ct).AsTask();

    public Task<Brand?> GetBrandByIdAsync(Guid id, CancellationToken ct = default)
        => _platform.Unfiltered(_context.Brands, PlatformQueryReason.SubscriberScopedExplicit)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _context.Brands.AsQueryable().Where(x => x.SubscriberId == subscriberId);
        if (onlyActive)
            q = q.Where(x => x.IsActive);
        return await q.OrderBy(x => x.Code).ToListAsync(ct);
    }

    public Task AddProductTypeAsync(ProductType type, CancellationToken ct = default)
        => _context.ProductTypes.AddAsync(type, ct).AsTask();

    public Task<ProductType?> GetProductTypeByIdAsync(Guid id, CancellationToken ct = default)
        => _platform.Unfiltered(_context.ProductTypes, PlatformQueryReason.SubscriberScopedExplicit)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ProductType>> GetProductTypesAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _context.ProductTypes.AsQueryable().Where(x => x.SubscriberId == subscriberId);
        if (onlyActive)
            q = q.Where(x => x.IsActive);
        return await q.OrderBy(x => x.Code).ToListAsync(ct);
    }

    public Task AddTariffAsync(Tariff tariff, CancellationToken ct = default)
        => _context.Tariffs.AddAsync(tariff, ct).AsTask();

    public async Task<IReadOnlyList<Tariff>> GetTariffsAsync(Guid subscriberId, bool onlyActive = true, CancellationToken ct = default)
    {
        var q = _context.Tariffs.AsQueryable().Where(x => x.SubscriberId == subscriberId);
        if (onlyActive)
            q = q.Where(x => x.IsActive);
        return await q.OrderBy(x => x.Code).ToListAsync(ct);
    }

    public Task AddProductLineAsync(ProductLine line, CancellationToken ct = default)
        => _context.ProductLines.AddAsync(line, ct).AsTask();

    public async Task<IReadOnlyList<ProductLine>> GetProductLinesAsync(Guid subscriberId, bool? activeFilter = true, string? search = null, CancellationToken ct = default)
    {
        var q = _context.ProductLines.AsQueryable().Where(x => x.SubscriberId == subscriberId);
        if (activeFilter is true)
            q = q.Where(x => x.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(x => x.Code.ToLower().Contains(s) || x.Name.ToLower().Contains(s));
        }

        return await q.OrderBy(x => x.Code).ToListAsync(ct);
    }

    public Task<ProductLine?> GetProductLineByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.ProductLines.FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.Id == id, ct);

    public Task<bool> ProductLineCodeExistsAsync(Guid subscriberId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = code.ToUpperInvariant();
        var q = _context.ProductLines.Where(x => x.SubscriberId == subscriberId && x.Code == normalized);
        if (excludeId is not null)
            q = q.Where(x => x.Id != excludeId);
        return q.AnyAsync(ct);
    }

    public Task<int> CountActiveCategoriesByLineAsync(Guid subscriberId, Guid lineId, CancellationToken ct = default)
        => _context.ProductCategories.CountAsync(
            x => x.SubscriberId == subscriberId && x.LineId == lineId && x.IsActive, ct);

    public Task AddProductCategoryAsync(ProductCategory category, CancellationToken ct = default)
        => _context.ProductCategories.AddAsync(category, ct).AsTask();

    public async Task<IReadOnlyList<ProductCategoryListRow>> GetProductCategoryListRowsAsync(
        Guid subscriberId, Guid? lineId = null, bool? activeFilter = true, string? search = null, CancellationToken ct = default)
    {
        var q =
            from c in _context.ProductCategories
            join l in _context.ProductLines on c.LineId equals l.Id
            where c.SubscriberId == subscriberId && l.SubscriberId == subscriberId
            select new { c, l };

        if (lineId is not null)
            q = q.Where(x => x.c.LineId == lineId);
        if (activeFilter is true)
            q = q.Where(x => x.c.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.c.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.c.Code.ToLower().Contains(s) ||
                x.c.Name.ToLower().Contains(s));
        }

        var list = await q
            .OrderBy(x => x.l.Code)
            .ThenBy(x => x.c.Code)
            .Select(x => new ProductCategoryListRow(
                x.c.Id,
                x.c.Code,
                x.c.Name,
                x.c.LineId,
                x.l.Code,
                x.l.Name,
                x.c.IsActive))
            .ToListAsync(ct);

        return list;
    }

    public Task<ProductCategory?> GetProductCategoryByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.ProductCategories.FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.Id == id, ct);

    public Task<bool> ProductCategoryCodeExistsAsync(Guid subscriberId, Guid lineId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = code.ToUpperInvariant();
        var q = _context.ProductCategories.Where(x =>
            x.SubscriberId == subscriberId && x.LineId == lineId && x.Code == normalized);
        if (excludeId is not null)
            q = q.Where(x => x.Id != excludeId);
        return q.AnyAsync(ct);
    }

    public Task<int> CountActiveSubcategoriesByCategoryAsync(Guid subscriberId, Guid categoryId, CancellationToken ct = default)
        => _context.ProductSubcategories.CountAsync(
            x => x.SubscriberId == subscriberId && x.CategoryId == categoryId && x.IsActive, ct);

    public Task AddProductSubcategoryAsync(ProductSubcategory subcategory, CancellationToken ct = default)
        => _context.ProductSubcategories.AddAsync(subcategory, ct).AsTask();

    public async Task<IReadOnlyList<ProductSubcategoryListRow>> GetProductSubcategoryListRowsAsync(
        Guid subscriberId, Guid? lineId = null, Guid? categoryId = null, bool? activeFilter = true, string? search = null, CancellationToken ct = default)
    {
        var q =
            from s in _context.ProductSubcategories
            join c in _context.ProductCategories on s.CategoryId equals c.Id
            join l in _context.ProductLines on c.LineId equals l.Id
            where s.SubscriberId == subscriberId && c.SubscriberId == subscriberId && l.SubscriberId == subscriberId
            select new { s, c, l };

        if (lineId is not null)
            q = q.Where(x => x.c.LineId == lineId);
        if (categoryId is not null)
            q = q.Where(x => x.s.CategoryId == categoryId);
        if (activeFilter is true)
            q = q.Where(x => x.s.IsActive);
        else if (activeFilter is false)
            q = q.Where(x => !x.s.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var t = search.Trim().ToLowerInvariant();
            q = q.Where(x =>
                x.s.Code.ToLower().Contains(t) ||
                x.s.Name.ToLower().Contains(t));
        }

        var list = await q
            .OrderBy(x => x.l.Code)
            .ThenBy(x => x.c.Code)
            .ThenBy(x => x.s.Code)
            .Select(x => new ProductSubcategoryListRow(
                x.s.Id,
                x.s.Code,
                x.s.Name,
                x.s.CategoryId,
                x.l.Id,
                x.l.Code,
                x.l.Name,
                x.c.Code,
                x.c.Name,
                x.s.IsActive))
            .ToListAsync(ct);

        return list;
    }

    public Task<ProductSubcategory?> GetProductSubcategoryByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.ProductSubcategories.FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.Id == id, ct);

    public Task<bool> ProductSubcategoryCodeExistsAsync(Guid subscriberId, Guid categoryId, string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = code.ToUpperInvariant();
        var q = _context.ProductSubcategories.Where(x =>
            x.SubscriberId == subscriberId && x.CategoryId == categoryId && x.Code == normalized);
        if (excludeId is not null)
            q = q.Where(x => x.Id != excludeId);
        return q.AnyAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}


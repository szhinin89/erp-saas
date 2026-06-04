using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Products.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class ItemCatalogRepository : IItemCatalogRepository
{
    private readonly ErpDbContext _context;

    public ItemCatalogRepository(ErpDbContext context) => _context = context;

    // ── ItemFamily ─────────────────────────────────────────────────────────

    public async Task<ItemFamily?> GetFamilyByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ItemFamilies.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<ItemFamily?> GetFamilyByCodeAsync(string code, Guid subscriberId, CancellationToken ct = default)
        => await _context.ItemFamilies
            .FirstOrDefaultAsync(x => x.SubscriberId == subscriberId && x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task<IReadOnlyList<ItemFamily>> GetFamiliesAsync(Guid subscriberId, CancellationToken ct = default)
        => await _context.ItemFamilies
            .Where(x => x.SubscriberId == subscriberId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<bool> FamilyCodeExistsAsync(string code, Guid subscriberId, CancellationToken ct = default)
        => await _context.ItemFamilies
            .AnyAsync(x => x.SubscriberId == subscriberId && x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task AddFamilyAsync(ItemFamily family, CancellationToken ct = default)
        => await _context.ItemFamilies.AddAsync(family, ct);

    // ── ItemCategory ───────────────────────────────────────────────────────

    public async Task<ItemCategory?> GetCategoryByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ItemCategories.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ItemCategory>> GetCategoriesAsync(
        Guid subscriberId, Guid? familyId = null, CancellationToken ct = default)
    {
        var q = _context.ItemCategories.Where(x => x.SubscriberId == subscriberId);
        if (familyId.HasValue) q = q.Where(x => x.FamilyId == familyId.Value);
        return await q.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<bool> CategoryCodeExistsAsync(
        string code, Guid familyId, Guid subscriberId, CancellationToken ct = default)
        => await _context.ItemCategories
            .AnyAsync(x => x.SubscriberId == subscriberId
                        && x.FamilyId == familyId
                        && x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task<int> CountActiveSubcategoriesByCategoryAsync(Guid categoryId, CancellationToken ct = default)
        => await _context.ItemSubcategories.CountAsync(x => x.CategoryId == categoryId && x.IsActive, ct);

    public async Task AddCategoryAsync(ItemCategory category, CancellationToken ct = default)
        => await _context.ItemCategories.AddAsync(category, ct);

    // ── ItemSubcategory ────────────────────────────────────────────────────

    public async Task<ItemSubcategory?> GetSubcategoryByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ItemSubcategories.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ItemSubcategory>> GetSubcategoriesAsync(
        Guid subscriberId, Guid? categoryId = null, CancellationToken ct = default)
    {
        var q = _context.ItemSubcategories.Where(x => x.SubscriberId == subscriberId);
        if (categoryId.HasValue) q = q.Where(x => x.CategoryId == categoryId.Value);
        return await q.OrderBy(x => x.Name).ToListAsync(ct);
    }

    public async Task<bool> SubcategoryCodeExistsAsync(
        string code, Guid categoryId, Guid subscriberId, CancellationToken ct = default)
        => await _context.ItemSubcategories
            .AnyAsync(x => x.SubscriberId == subscriberId
                        && x.CategoryId == categoryId
                        && x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task AddSubcategoryAsync(ItemSubcategory sub, CancellationToken ct = default)
        => await _context.ItemSubcategories.AddAsync(sub, ct);

    // ── Brand ──────────────────────────────────────────────────────────────

    public async Task<Brand?> GetBrandByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Brands.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<Brand>> GetBrandsAsync(Guid subscriberId, CancellationToken ct = default)
        => await _context.Brands
            .Where(x => x.SubscriberId == subscriberId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

    public async Task<bool> BrandCodeExistsAsync(string code, Guid subscriberId, CancellationToken ct = default)
        => await _context.Brands
            .AnyAsync(x => x.SubscriberId == subscriberId
                        && x.Code == code.Trim().ToUpperInvariant(), ct);

    public async Task AddBrandAsync(Brand brand, CancellationToken ct = default)
        => await _context.Brands.AddAsync(brand, ct);

    // ── Shared ─────────────────────────────────────────────────────────────

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);
}

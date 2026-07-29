using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using ERP.Domain.Modules.Items.Models;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class ItemRepository : IItemRepository
{
    private readonly ErpDbContext _context;

    public ItemRepository(ErpDbContext context) => _context = context;

    // Item es tenant-scoped (sin CompanyId) — filtro simple por tenantId.
    // El global query filter de EF ya aplica el filtro automático;
    // el parámetro tenantId es para casos en que se llama sin contexto HTTP.
    private IQueryable<Item> Scoped(Guid tenantId)
        => _context.Items.Where(x => x.TenantId == tenantId);

    public async Task<Item?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Scoped(tenantId)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Attributes)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Barcodes)
            .Include(x => x.Images)
            .Include(x => x.UnitConversions)
            .Include(x => x.Substitutes)
            .Include(x => x.PackagingLevels)
            .Include(x => x.SupplierCodes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Item?> GetByIdLightAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => await Scoped(tenantId)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Item>> GetByIdsLightAsync(IReadOnlyCollection<Guid> ids, Guid tenantId, CancellationToken cancellationToken = default)
        => ids.Count == 0
            ? []
            : await Scoped(tenantId).Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);

    public async Task<Item?> GetBySkuAsync(string sku, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return await Scoped(tenantId)
            .Include(x => x.Variants).ThenInclude(v => v.Attributes)
            .Include(x => x.Variants).ThenInclude(v => v.Barcodes)
            .Include(x => x.Images)
            .Include(x => x.UnitConversions)
            .Include(x => x.Substitutes)
            .Include(x => x.PackagingLevels)
            .Include(x => x.SupplierCodes)
            .FirstOrDefaultAsync(x => x.Code.SKU == normalized, cancellationToken);
    }

    public async Task<Item?> ResolveByAnyCodeAsync(string code, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var c = code.Trim();
        var upper = c.ToUpperInvariant();

        IQueryable<Item> Full() => Scoped(tenantId)
            .Include(x => x.Variants).ThenInclude(v => v.Attributes)
            .Include(x => x.Variants).ThenInclude(v => v.Barcodes)
            .Include(x => x.Images)
            .Include(x => x.UnitConversions)
            .Include(x => x.Substitutes)
            .Include(x => x.PackagingLevels)
            .Include(x => x.SupplierCodes);

        return await Full().FirstOrDefaultAsync(x => x.Code.SKU == upper, cancellationToken)
            ?? await Full().FirstOrDefaultAsync(x => x.Variants.Any(v => v.SKU == upper && v.IsActive), cancellationToken)
            ?? await Full().FirstOrDefaultAsync(x => x.Variants.Any(v => v.Barcodes.Any(b => b.Code == c && b.IsActive)), cancellationToken)
            ?? await Full().FirstOrDefaultAsync(x => x.PackagingLevels.Any(p => p.Barcode == c && p.IsActive), cancellationToken);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, Guid tenantId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        var query = Scoped(tenantId).Where(x => x.Code.SKU == normalized);
        if (excludeId.HasValue)
            query = query.Where(x => x.Id != excludeId.Value);
        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> BarcodeExistsAsync(string code, Guid tenantId, CancellationToken cancellationToken = default)
        => await _context.ItemVariantBarcodes
            .AnyAsync(b => b.TenantId == tenantId && b.Code == code && b.IsActive, cancellationToken);

    public async Task<bool> SupplierCodeExistsAsync(Guid supplierId, string code, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return await _context.Set<ItemSupplierCode>()
            .AnyAsync(s => s.TenantId == tenantId && s.SupplierId == supplierId && s.Code == normalized && s.IsActive, cancellationToken);
    }

    public async Task<bool> VariantSkuExistsAsync(string sku, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return await _context.Set<ItemVariant>()
            .AnyAsync(v => v.TenantId == tenantId && v.SKU == normalized && v.IsActive, cancellationToken);
    }

    public async Task<string?> GetSupplierCodeAsync(Guid itemId, Guid supplierId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var codes = await _context.Set<ItemSupplierCode>()
            .Where(s => s.TenantId == tenantId && s.ItemId == itemId && s.SupplierId == supplierId && s.IsActive)
            .ToListAsync(cancellationToken);

        return codes.FirstOrDefault(s => s.IsPrimary)?.Code ?? codes.FirstOrDefault()?.Code;
    }

    public async Task<Guid?> FindItemIdBySupplierCodeAsync(Guid supplierId, string code, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        // 1. Regla principal:
        // Código específico del proveedor
        var supplierItemId = await _context.Set<ItemSupplierCode>()
            .Where(s =>
                s.TenantId == tenantId &&
                s.SupplierId == supplierId &&
                s.Code == normalized &&
                s.IsActive)
            .Select(s => (Guid?)s.ItemId)
            .FirstOrDefaultAsync(cancellationToken);

        if (supplierItemId.HasValue)
            return supplierItemId;

        // 2. Fallback:
        // Si el código del XML coincide con el SKU interno
        return await Scoped(tenantId)
            .Where(x =>
                x.IsActive &&
                x.Code.SKU == normalized)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ItemSimilarityMatch>> SearchBySimilarityAsync(
        string text, Guid tenantId, int maxResults, double minScore, CancellationToken cancellationToken = default)
    {
        var results = await Scoped(tenantId)
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Id,
                x.Code.SKU,
                x.Code.ShortName,
                x.Code.Description,
                Score = EF.Functions.TrigramsSimilarity(x.Code.Description, text) > EF.Functions.TrigramsSimilarity(x.Code.ShortName, text)
                    ? EF.Functions.TrigramsSimilarity(x.Code.Description, text)
                    : EF.Functions.TrigramsSimilarity(x.Code.ShortName, text),
            })
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return results
            .Select(x => new ItemSimilarityMatch(x.Id, x.SKU, x.ShortName, x.Description, (double)x.Score))
            .ToList();
    }

    public async Task<(IReadOnlyList<Item> Items, int TotalCount)> GetPageAsync(
        Guid tenantId,
        ItemReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var query = Scoped(tenantId);

        if (filter.IsActive is not null)
            query = query.Where(x => x.IsActive == filter.IsActive);

        if (filter.ItemTypeId is not null)
            query = query.Where(x => x.ItemTypeId == filter.ItemTypeId);

        if (filter.CategoryNodeId is not null)
            query = query.Where(x => x.CategoryNodeId == filter.CategoryNodeId);

        if (filter.BrandId is not null)
            query = query.Where(x => x.BrandId == filter.BrandId);

        if (filter.IsForSale is not null)
            query = query.Where(x => x.SaleConfig.IsForSale == filter.IsForSale);

        if (filter.IsFavorite is not null)
            query = query.Where(x => x.SaleConfig.IsFavorite == filter.IsFavorite);

        if (filter.IsEcommerce is not null)
            query = query.Where(x => x.SaleConfig.IsEcommerceActive == filter.IsEcommerce);

        if (!string.IsNullOrWhiteSpace(filter.Sku))
        {
            var sku = filter.Sku.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.SKU == sku);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = $"%{filter.Search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Code.SKU, s) ||
                EF.Functions.ILike(x.Code.ShortName, s) ||
                EF.Functions.ILike(x.Code.Description, s));
        }

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var code = filter.Barcode.Trim();
            query = query
                .Include(x => x.Variants).ThenInclude(v => v.Barcodes)
                .Include(x => x.PackagingLevels)
                .Where(x =>
                    x.Variants.Any(v => v.Barcodes.Any(b => b.Code == code && b.IsActive))
                    || x.PackagingLevels.Any(p => p.Barcode == code && p.IsActive));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.Code.SKU)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<IReadOnlyList<Item>> GetAllActiveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await Scoped(tenantId)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code.SKU)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default)
        => await _context.Items.AddAsync(item, cancellationToken);

    public async Task TrackVariantAsync(ItemVariant variant, CancellationToken cancellationToken = default)
        => await _context.ItemVariants.AddAsync(variant, cancellationToken);

    public async Task ReplaceImagesAsync(Guid itemId, IEnumerable<ItemImage> newImages, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ItemImages.Where(x => x.ItemId == itemId).ToListAsync(cancellationToken);
        _context.ItemImages.RemoveRange(existing);
        await _context.ItemImages.AddRangeAsync(newImages, cancellationToken);
    }

    public async Task ReplaceUnitConversionsAsync(Guid itemId, IEnumerable<ItemUnitConversion> newConversions, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ItemUnitConversions.Where(x => x.ItemId == itemId).ToListAsync(cancellationToken);
        _context.ItemUnitConversions.RemoveRange(existing);
        await _context.ItemUnitConversions.AddRangeAsync(newConversions, cancellationToken);
    }

    public async Task ReplaceSubstitutesAsync(Guid itemId, IEnumerable<ItemSubstitute> newSubstitutes, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ItemSubstitutes.Where(x => x.ItemId == itemId).ToListAsync(cancellationToken);
        _context.ItemSubstitutes.RemoveRange(existing);
        await _context.ItemSubstitutes.AddRangeAsync(newSubstitutes, cancellationToken);
    }

    public async Task ReplacePackagingLevelsAsync(Guid itemId, IEnumerable<ItemPackagingLevel> newLevels, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ItemPackagingLevels.Where(x => x.ItemId == itemId).ToListAsync(cancellationToken);
        _context.ItemPackagingLevels.RemoveRange(existing);
        await _context.ItemPackagingLevels.AddRangeAsync(newLevels, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}

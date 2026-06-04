using ERP.Application.Common;
using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Items;

public sealed class ItemRepository : IItemRepository
{
    private readonly ErpDbContext _context;

    public ItemRepository(ErpDbContext context) => _context = context;

    // Item es subscriber-scoped (sin CompanyId) — filtro simple por SubscriberId.
    // El global query filter de EF ya aplica el filtro automático;
    // el parámetro subscriberId es para casos en que se llama sin contexto HTTP.
    private IQueryable<Item> Scoped(Guid subscriberId)
        => _context.Items.Where(x => x.SubscriberId == subscriberId);

    public async Task<Item?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await Scoped(subscriberId)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Attributes)
            .Include(x => x.Variants)
                .ThenInclude(v => v.Barcodes)
            .Include(x => x.Images)
            .Include(x => x.UnitConversions)
            .Include(x => x.Substitutes)
            .Include(x => x.PackagingLevels)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Item?> GetByIdLightAsync(Guid id, Guid subscriberId, CancellationToken ct = default)
        => await Scoped(subscriberId)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<Item?> GetBySkuAsync(string sku, Guid subscriberId, CancellationToken ct = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return await Scoped(subscriberId)
            .Include(x => x.Variants).ThenInclude(v => v.Attributes)
            .Include(x => x.Variants).ThenInclude(v => v.Barcodes)
            .Include(x => x.Images)
            .Include(x => x.UnitConversions)
            .Include(x => x.Substitutes)
            .Include(x => x.PackagingLevels)
            .FirstOrDefaultAsync(x => x.Code.SKU == normalized, ct);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, Guid subscriberId, CancellationToken ct = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return await Scoped(subscriberId)
            .AnyAsync(x => x.Code.SKU == normalized, ct);
    }

    public async Task<(IReadOnlyList<Item> Items, int TotalCount)> GetPageAsync(
        Guid subscriberId,
        ItemReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1)   pageSize   = 20;
        if (pageSize > 200) pageSize   = 200;

        var query = Scoped(subscriberId);

        if (filter.IsActive is not null)
            query = query.Where(x => x.IsActive == filter.IsActive);

        if (filter.ItemType is not null)
            query = query.Where(x => x.ItemType == filter.ItemType);

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
            var s = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.SKU.ToLower().Contains(s) ||
                x.Code.ShortName.ToLower().Contains(s) ||
                x.Code.Description.ToLower().Contains(s) ||
                (x.Code.PurchaseCode != null && x.Code.PurchaseCode.ToLower().Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Barcode))
        {
            var code = filter.Barcode.Trim();
            query = query
                .Include(x => x.Variants).ThenInclude(v => v.Barcodes)
                .Where(x => x.Variants.Any(v => v.Barcodes.Any(b => b.Code == code && b.IsActive)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.Code.SKU)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Item>> GetAllActiveAsync(Guid subscriberId, CancellationToken ct = default)
        => await Scoped(subscriberId)
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code.SKU)
            .ToListAsync(ct);

    public async Task AddAsync(Item item, CancellationToken ct = default)
        => await _context.Items.AddAsync(item, ct);

    public async Task TrackVariantAsync(ItemVariant variant, CancellationToken ct = default)
        => await _context.ItemVariants.AddAsync(variant, ct);

    public async Task ReplaceImagesAsync(Guid itemId, IEnumerable<ItemImage> newImages, CancellationToken ct = default)
    {
        var existing = await _context.ItemImages.Where(x => x.ItemId == itemId).ToListAsync(ct);
        _context.ItemImages.RemoveRange(existing);
        await _context.ItemImages.AddRangeAsync(newImages, ct);
    }

    public async Task ReplaceUnitConversionsAsync(Guid itemId, IEnumerable<ItemUnitConversion> newConversions, CancellationToken ct = default)
    {
        var existing = await _context.ItemUnitConversions.Where(x => x.ItemId == itemId).ToListAsync(ct);
        _context.ItemUnitConversions.RemoveRange(existing);
        await _context.ItemUnitConversions.AddRangeAsync(newConversions, ct);
    }

    public async Task ReplaceSubstitutesAsync(Guid itemId, IEnumerable<ItemSubstitute> newSubstitutes, CancellationToken ct = default)
    {
        var existing = await _context.ItemSubstitutes.Where(x => x.ItemId == itemId).ToListAsync(ct);
        _context.ItemSubstitutes.RemoveRange(existing);
        await _context.ItemSubstitutes.AddRangeAsync(newSubstitutes, ct);
    }

    public async Task ReplacePackagingLevelsAsync(Guid itemId, IEnumerable<ItemPackagingLevel> newLevels, CancellationToken ct = default)
    {
        var existing = await _context.ItemPackagingLevels.Where(x => x.ItemId == itemId).ToListAsync(ct);
        _context.ItemPackagingLevels.RemoveRange(existing);
        await _context.ItemPackagingLevels.AddRangeAsync(newLevels, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<StockLedgerRow>> GetStockByItemAsync(
        Guid itemId, Guid? warehouseId, CancellationToken ct = default)
    {
        // NOTE: Transitional — uses current_stock (ProductId) until stock_ledger migration.
        // In new purchases processed through Items BC, ProductId = ItemId.
        var query = _context.CurrentStocks
            .Where(s => s.ProductId == itemId);

        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);

        return await query
            .Select(s => new StockLedgerRow(
                s.ProductId,     // ItemId
                null,            // VariantId — not in CurrentStock yet
                s.WarehouseId,
                s.Quantity,
                s.ReservedQuantity,
                s.AvailableQuantity,
                s.TotalStockValue,
                s.LastUpdatedAt))
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<KardexRow> Rows, int TotalCount)> GetKardexAsync(
        Guid itemId, Guid? variantId, Guid? warehouseId,
        DateTime? fromUtc, DateTime? toUtc,
        int pageNumber, int pageSize,
        CancellationToken ct = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1)   pageSize   = 50;
        if (pageSize > 500) pageSize   = 500;

        // NOTE: Transitional — uses stock_movement (ProductId = ItemId).
        var q = _context.StockMovements.Where(m => m.ProductId == itemId);

        if (warehouseId.HasValue) q = q.Where(m => m.WarehouseId == warehouseId.Value);
        if (fromUtc.HasValue)     q = q.Where(m => m.CreatedAt >= fromUtc.Value);
        if (toUtc.HasValue)       q = q.Where(m => m.CreatedAt <= toUtc.Value);

        var total = await q.CountAsync(ct);

        var rows = await q
            .OrderByDescending(m => m.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new KardexRow(
                m.Id,
                m.ProductId,      // ItemId (transitional)
                null,             // VariantId — pending migration
                m.WarehouseId,
                null,             // LotId — pending migration
                null,             // SerialId — pending migration
                m.MovementType.ToString(),
                1,                // Direction — positive/negative inferred from MovementType
                m.Quantity,
                m.PreviousQuantity,
                m.ResultQuantity,
                m.UnitCost,
                m.TotalCost,
                m.Reference,
                m.SourceDocType,
                m.CreatedAt))
            .ToListAsync(ct);

        return (rows, total);
    }
}

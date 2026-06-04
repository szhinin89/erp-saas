using ERP.Domain.Modules.Items.Entities;

namespace ERP.Domain.Modules.Items.Interfaces;

public interface IItemRepository
{
    /// <summary>Carga completa con variantes, imágenes, conversiones, sustitutos y empaques.</summary>
    Task<Item?> GetByIdAsync(Guid id, Guid subscriberId, CancellationToken ct = default);

    /// <summary>Carga ligera: solo propiedades escalares (sin colecciones).</summary>
    Task<Item?> GetByIdLightAsync(Guid id, Guid subscriberId, CancellationToken ct = default);

    Task<Item?> GetBySkuAsync(string sku, Guid subscriberId, CancellationToken ct = default);

    Task<bool> ExistsBySkuAsync(string sku, Guid subscriberId, CancellationToken ct = default);

    Task<(IReadOnlyList<Item> Items, int TotalCount)> GetPageAsync(
        Guid subscriberId,
        ItemReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<Item>> GetAllActiveAsync(Guid subscriberId, CancellationToken ct = default);

    Task AddAsync(Item item, CancellationToken ct = default);

    /// <summary>
    /// Explicitly registers a new ItemVariant with the change tracker.
    /// Required because EF Core cannot detect entities added to private backing fields (List&lt;T&gt;).
    /// </summary>
    Task TrackVariantAsync(ItemVariant variant, CancellationToken ct = default);

    // ── Replace operations (bypass backing field limitation) ──────────────

    Task ReplaceImagesAsync(Guid itemId, IEnumerable<ItemImage> newImages, CancellationToken ct = default);
    Task ReplaceUnitConversionsAsync(Guid itemId, IEnumerable<ItemUnitConversion> newConversions, CancellationToken ct = default);
    Task ReplaceSubstitutesAsync(Guid itemId, IEnumerable<ItemSubstitute> newSubstitutes, CancellationToken ct = default);
    Task ReplacePackagingLevelsAsync(Guid itemId, IEnumerable<ItemPackagingLevel> newLevels, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    // ── Stock queries ──────────────────────────────────────────────────────

    Task<IReadOnlyList<StockLedgerRow>> GetStockByItemAsync(
        Guid itemId, Guid? warehouseId, CancellationToken ct = default);

    Task<(IReadOnlyList<KardexRow> Rows, int TotalCount)> GetKardexAsync(
        Guid itemId, Guid? variantId, Guid? warehouseId,
        DateTime? fromUtc, DateTime? toUtc,
        int pageNumber, int pageSize,
        CancellationToken ct = default);
}

// ── Projection types ───────────────────────────────────────────────────────

public record StockLedgerRow(
    Guid     ItemId,
    Guid?    VariantId,
    Guid     WarehouseId,
    decimal  Quantity,
    decimal  ReservedQuantity,
    decimal  AvailableQuantity,
    decimal  TotalCostValue,
    DateTime LastUpdatedAt
);

public record KardexRow(
    Guid      MovementId,
    Guid      ItemId,
    Guid?     VariantId,
    Guid      WarehouseId,
    Guid?     LotId,
    Guid?     SerialId,
    string    MovementType,
    int       Direction,
    decimal   Quantity,
    decimal   PreviousQuantity,
    decimal   ResultQuantity,
    decimal?  UnitCost,
    decimal?  TotalCost,
    string?   Reference,
    string?   SourceDocumentType,
    DateTime  MovementDate
);

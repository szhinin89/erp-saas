using ERP.Domain.Modules.Items.Entities;
using ERP.Domain.Modules.Items.Models;

namespace ERP.Domain.Modules.Items.Interfaces;

public interface IItemRepository
{
    /// <summary>Carga completa con variantes, imágenes, conversiones, sustitutos y empaques.</summary>
    Task<Item?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Carga ligera: solo propiedades escalares (sin colecciones).</summary>
    Task<Item?> GetByIdLightAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Carga ligera en batch (una sola query, sin N+1) — para read-models que necesitan varios ítems por Id.</summary>
    Task<IReadOnlyList<Item>> GetByIdsLightAsync(IReadOnlyCollection<Guid> ids, Guid tenantId, CancellationToken cancellationToken = default);

    Task<Item?> GetBySkuAsync(string sku, Guid tenantId, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySkuAsync(string sku, Guid tenantId, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Código de barras único por tenant (todo el catálogo, no solo el ítem actual).</summary>
    Task<bool> BarcodeExistsAsync(string code, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>Código de proveedor único por (tenant, supplier) en todo el catálogo.</summary>
    Task<bool> SupplierCodeExistsAsync(Guid supplierId, string code, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>SKU de variante único por tenant (todo el catálogo, no solo el ítem actual).</summary>
    Task<bool> VariantSkuExistsAsync(string sku, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resuelve el código de proveedor de un ítem para un proveedor específico (Fase 8).
    /// Prefiere el marcado como principal si existen varios; null si el ítem no tiene
    /// ningún código registrado para ese proveedor.
    /// </summary>
    Task<string?> GetSupplierCodeAsync(Guid itemId, Guid supplierId, Guid tenantId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Item> Items, int TotalCount)> GetPageAsync(
        Guid tenantId,
        ItemReportFilter filter,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Item>> GetAllActiveAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<Item?> ResolveByAnyCodeAsync(string code, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Item Matching (Purchase Reception): resuelve el ítem que ya tiene un código de proveedor
    /// exacto registrado para (supplierId, code). Null si no hay ninguno activo.
    /// </summary>
    Task<Guid?> FindItemIdBySupplierCodeAsync(Guid supplierId, string code, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Item Matching (Purchase Reception): candidatos por similitud de texto (pg_trgm) contra
    /// SKU/nombre corto/descripción, ordenados por score descendente. Solo activos.
    /// </summary>
    Task<IReadOnlyList<ItemSimilarityMatch>> SearchBySimilarityAsync(
        string text, Guid tenantId, int maxResults, double minScore, CancellationToken cancellationToken = default);

    Task AddAsync(Item item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly registers a new ItemVariant with the change tracker.
    /// Required because EF Core cannot detect entities added to private backing fields (List&lt;T&gt;).
    /// </summary>
    Task TrackVariantAsync(ItemVariant variant, CancellationToken cancellationToken = default);

    // ── Replace operations (bypass backing field limitation) ──────────────

    Task ReplaceImagesAsync(Guid itemId, IEnumerable<ItemImage> newImages, CancellationToken cancellationToken = default);
    Task ReplaceUnitConversionsAsync(Guid itemId, IEnumerable<ItemUnitConversion> newConversions, CancellationToken cancellationToken = default);
    Task ReplaceSubstitutesAsync(Guid itemId, IEnumerable<ItemSubstitute> newSubstitutes, CancellationToken cancellationToken = default);
    Task ReplacePackagingLevelsAsync(Guid itemId, IEnumerable<ItemPackagingLevel> newLevels, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

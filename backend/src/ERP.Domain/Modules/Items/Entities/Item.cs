using ERP.Domain.Common;
using ERP.Domain.Modules.Items.Enums;
using ERP.Domain.Modules.Items.Events;
using ERP.Domain.Modules.Items.ValueObjects;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Aggregate Root del catálogo de ítems.
/// Scope: SubscriberId only — compartido entre todas las companies del subscriber.
/// REGLA: SKU es inmutable. Stock, costo y precio nunca viven aquí.
/// </summary>
public sealed class Item : MasterEntity, ISubscriberScopedEntity
{
    private readonly List<ItemVariant> _variants = new();
    private readonly List<ItemImage> _images = new();
    private readonly List<ItemUnitConversion> _unitConversions = new();
    private readonly List<ItemSubstitute> _substitutes = new();
    private readonly List<ItemPackagingLevel> _packagingLevels = new();

    // ── Identidad ─────────────────────────────────────────────────────────
    public ItemCode Code { get; private set; } = null!;
    public ItemType ItemType { get; private set; }
    public string? Observations { get; private set; }

    // ── Clasificación ─────────────────────────────────────────────────────
    public Guid? CategoryNodeId { get; private set; }
    public Guid? BrandId { get; private set; }

    // ── Unidad base ───────────────────────────────────────────────────────
    public string DefaultUomCode { get; private set; } = null!;

    // ── Value Objects ─────────────────────────────────────────────────────
    public ItemTaxConfig TaxConfig { get; private set; } = null!;
    public ItemSaleConfig SaleConfig { get; private set; } = null!;
    public ItemStockConfig StockConfig { get; private set; } = null!;

    // ── JSONB flexible ────────────────────────────────────────────────────
    // Almacenados como JSON en BD; clave=código de AttributeDefinition
    public Dictionary<string, object?> Specifications { get; private set; } = new();
    public Dictionary<string, object?> MarketingAttributes { get; private set; } = new();

    // ── Colecciones ───────────────────────────────────────────────────────
    public IReadOnlyList<ItemVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyList<ItemImage> Images => _images.AsReadOnly();
    public IReadOnlyList<ItemUnitConversion> UnitConversions => _unitConversions.AsReadOnly();
    public IReadOnlyList<ItemSubstitute> Substitutes => _substitutes.AsReadOnly();
    public IReadOnlyList<ItemPackagingLevel> PackagingLevels => _packagingLevels.AsReadOnly();

    private Item() { }

    // ── Factory ───────────────────────────────────────────────────────────

    public static Item Create(
        Guid subscriberId,
        string sku,
        string shortName,
        string description,
        ItemType itemType,
        string defaultUomCode,
        ItemTaxConfig taxConfig,
        ItemSaleConfig saleConfig,
        ItemStockConfig stockConfig,
        Guid createdBy,
        Guid? categoryNodeId = null,
        Guid? brandId = null,
        string? observations = null)
    {
        if (string.IsNullOrWhiteSpace(defaultUomCode))
            throw new ArgumentException("La unidad de medida base es obligatoria.", nameof(defaultUomCode));

        var code = ItemCode.Create(sku, shortName, description);

        var item = new Item
        {
            SubscriberId     = subscriberId,
            Code             = code,
            ItemType         = itemType,
            DefaultUomCode   = defaultUomCode.Trim().ToUpperInvariant(),
            TaxConfig        = taxConfig,
            SaleConfig       = saleConfig,
            StockConfig      = stockConfig,
            CategoryNodeId   = categoryNodeId,
            BrandId          = brandId,
            Observations     = observations?.Trim(),
        };

        item.SetCreated(createdBy);
        item.RaiseDomainEvent(new ItemCreatedEvent(item.Id, code.SKU, itemType, subscriberId));
        return item;
    }

    // ── Actualización ─────────────────────────────────────────────────────

    public void UpdateIdentity(
        string shortName, string description, string? observations, Guid updatedBy)
    {
        // SKU es inmutable — solo se actualiza shortName, description y observations
        Code         = ItemCode.Create(Code.SKU, shortName, description, Code.PurchaseCode);
        Observations = observations?.Trim();
        SetUpdated(updatedBy);
    }

    public void UpdatePurchaseCode(string? purchaseCode, Guid updatedBy)
    {
        Code = ItemCode.Create(Code.SKU, Code.ShortName, Code.Description, purchaseCode);
        SetUpdated(updatedBy);
    }

    public void UpdateClassification(Guid? categoryNodeId, Guid? brandId, Guid updatedBy)
    {
        CategoryNodeId = categoryNodeId;
        BrandId        = brandId;
        SetUpdated(updatedBy);
    }

    public void UpdateDefaultUom(string uomCode, Guid updatedBy)
    {
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("La unidad de medida no puede estar vacía.", nameof(uomCode));
        DefaultUomCode = uomCode.Trim().ToUpperInvariant();
        SetUpdated(updatedBy);
    }

    public void UpdateTaxConfig(ItemTaxConfig taxConfig, Guid updatedBy)
    {
        TaxConfig = taxConfig;
        SetUpdated(updatedBy);
    }

    public void UpdateSaleConfig(ItemSaleConfig saleConfig, Guid updatedBy)
    {
        SaleConfig = saleConfig;
        SetUpdated(updatedBy);
    }

    public void UpdateStockConfig(ItemStockConfig stockConfig, Guid updatedBy)
    {
        StockConfig = stockConfig;
        SetUpdated(updatedBy);
    }

    public void UpdateSpecifications(Dictionary<string, object?> specs, Guid updatedBy)
    {
        Specifications = specs;
        SetUpdated(updatedBy);
    }

    public void UpdateMarketingAttributes(Dictionary<string, object?> attrs, Guid updatedBy)
    {
        MarketingAttributes = attrs;
        SetUpdated(updatedBy);
    }

    // ── Variantes ─────────────────────────────────────────────────────────

    public ItemVariant AddVariant(
        IReadOnlyList<(Guid AttributeDefinitionId, string Value)> axisAttributes,
        string? skuOverride,
        int sortOrder,
        Guid updatedBy)
    {
        // Genera SKU de variante si no se especifica
        var variantSku = skuOverride?.Trim()
            ?? $"{Code.SKU}-{string.Join("-", axisAttributes.Select(a => a.Value.ToUpperInvariant()))}";

        if (_variants.Any(v => v.IsActive && v.SKU == variantSku))
            throw new InvalidOperationException($"Ya existe una variante con SKU '{variantSku}'.");

        // Validar unicidad de combinación de atributos de eje
        var newCombo = axisAttributes
            .OrderBy(a => a.AttributeDefinitionId)
            .Select(a => $"{a.AttributeDefinitionId}:{a.Value.ToUpperInvariant()}")
            .ToList();

        var duplicate = _variants.Any(v =>
            v.IsActive &&
            v.Attributes
                .OrderBy(a => a.AttributeDefinitionId)
                .Select(a => $"{a.AttributeDefinitionId}:{a.Value.ToUpperInvariant()}")
                .SequenceEqual(newCombo));

        if (duplicate)
            throw new InvalidOperationException("Ya existe una variante con la misma combinación de atributos.");

        var isDefault = !_variants.Any(v => v.IsActive);
        var variant = ItemVariant.Create(Id, SubscriberId, variantSku, axisAttributes, isDefault, sortOrder);
        _variants.Add(variant);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new ItemVariantAddedEvent(Id, variant.Id, variant.SKU, SubscriberId));
        return variant;
    }

    public void DisableVariant(Guid variantId, Guid updatedBy)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new InvalidOperationException("Variante no encontrada.");

        variant.Disable(updatedBy);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new ItemVariantDisabledEvent(Id, variantId, SubscriberId));
    }

    public void EnableVariant(Guid variantId, Guid updatedBy)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new InvalidOperationException("Variante no encontrada.");

        variant.Enable(updatedBy);
        SetUpdated(updatedBy);
    }

    // ── Imágenes ──────────────────────────────────────────────────────────

    public void ReplaceImages(
        IEnumerable<(Guid StorageObjectId, string? AltText, bool IsMain, bool IsEcommerce, int SortOrder)> images,
        Guid updatedBy)
    {
        var list = images.ToList();
        if (list.Count(i => i.IsMain) > 1)
            throw new InvalidOperationException("Solo una imagen puede ser principal.");

        _images.Clear();
        foreach (var i in list.OrderBy(x => x.SortOrder))
            _images.Add(ItemImage.Create(Id, SubscriberId, i.StorageObjectId, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder));

        SetUpdated(updatedBy);
    }

    // ── Conversiones de UOM ───────────────────────────────────────────────

    public void ReplaceUnitConversions(
        IEnumerable<(string FromUomCode, string ToUomCode, decimal Factor)> conversions,
        Guid updatedBy)
    {
        _unitConversions.Clear();
        foreach (var c in conversions)
            _unitConversions.Add(ItemUnitConversion.Create(Id, SubscriberId, c.FromUomCode, c.ToUomCode, c.Factor));
        SetUpdated(updatedBy);
    }

    // ── Sustitutos ────────────────────────────────────────────────────────

    public void ReplaceSubstitutes(
        IEnumerable<(Guid SubstituteItemId, int Priority, string? Note)> substitutes,
        Guid updatedBy)
    {
        _substitutes.Clear();
        foreach (var s in substitutes)
            _substitutes.Add(ItemSubstitute.Create(Id, SubscriberId, s.SubstituteItemId, s.Priority, s.Note));
        SetUpdated(updatedBy);
    }

    // ── Niveles de empaque ────────────────────────────────────────────────

    public void ReplacePackagingLevels(
        IEnumerable<(string Name, int Level, decimal BaseQuantity, string UomCode, string? Barcode, decimal? Weight,
            bool IsBaseUnit, bool IsPurchaseDefault, bool IsSaleDefault)> levels,
        Guid updatedBy)
    {
        var list = levels.ToList();
        if (list.Count(l => l.IsBaseUnit) != 1)
            throw new InvalidOperationException("Debe existir exactamente un nivel base (IsBaseUnit=true).");

        _packagingLevels.Clear();
        foreach (var l in list.OrderBy(x => x.Level))
            _packagingLevels.Add(ItemPackagingLevel.Create(
                Id, SubscriberId,
                l.Name, l.Level, l.BaseQuantity, l.UomCode,
                l.Barcode, l.Weight, l.IsBaseUnit, l.IsPurchaseDefault, l.IsSaleDefault));
        SetUpdated(updatedBy);
    }

    // ── Disable / Enable con invariante de negocio ────────────────────────

    public new void Disable(Guid updatedBy)
    {
        base.Disable(updatedBy);
        RaiseDomainEvent(new ItemDisabledEvent(Id, SubscriberId));
    }

    public new void Enable(Guid updatedBy)
    {
        base.Enable(updatedBy);
        RaiseDomainEvent(new ItemEnabledEvent(Id, SubscriberId));
    }
}

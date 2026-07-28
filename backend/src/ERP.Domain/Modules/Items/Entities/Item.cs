using ERP.Domain.Common;
using ERP.Domain.Modules.Items.Events;
using ERP.Domain.Modules.Items.ValueObjects;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Aggregate Root del catálogo de ítems.
/// Scope: tenantId only — compartido entre todas las companies del tenant.
/// REGLA: SKU es inmutable. Stock, costo y precio nunca viven aquí.
/// </summary>
public sealed class Item : MasterEntity, ITenantScopedEntity
{
    private readonly List<ItemVariant> _variants = new();
    private readonly List<ItemImage> _images = new();
    private readonly List<ItemUnitConversion> _unitConversions = new();
    private readonly List<ItemSubstitute> _substitutes = new();
    private readonly List<ItemPackagingLevel> _packagingLevels = new();
    private readonly List<ItemSupplierCode> _supplierCodes = new();

    // ── Identidad ─────────────────────────────────────────────────────────
    public ItemCode Code { get; private set; } = null!;
    public Guid ItemTypeId { get; private set; }
    public string? Observations { get; private set; }

    // ── Clasificación ─────────────────────────────────────────────────────
    public Guid? CategoryNodeId { get; private set; }
    public Guid? BrandId { get; private set; }

    // ── Unidad base ───────────────────────────────────────────────────────
    public string DefaultUomCode { get; private set; } = null!;

    // ── Precio base (SSOT, Motor de Pricing v2) ──────────────────────────
    // Único dueño del precio base del ítem. Las PriceList/PricingRule transforman
    // este valor — nunca lo reemplazan como fuente de verdad. Null = sin precio
    // configurado (error de configuración para PricingResolver, no un valor a inventar).
    public decimal? BaseSalePrice { get; private set; }

    // ── Value Objects ─────────────────────────────────────────────────────
    public ItemTaxConfig TaxConfig { get; private set; } = null!;
    public ItemSaleConfig SaleConfig { get; private set; } = null!;
    public ItemStockConfig StockConfig { get; private set; } = null!;

    // ── Colecciones ───────────────────────────────────────────────────────
    public IReadOnlyList<ItemVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyList<ItemImage> Images => _images.AsReadOnly();
    public IReadOnlyList<ItemUnitConversion> UnitConversions => _unitConversions.AsReadOnly();
    public IReadOnlyList<ItemSubstitute> Substitutes => _substitutes.AsReadOnly();
    public IReadOnlyList<ItemPackagingLevel> PackagingLevels => _packagingLevels.AsReadOnly();
    public IReadOnlyList<ItemSupplierCode> SupplierCodes => _supplierCodes.AsReadOnly();

    private Item() { }

    // ── Factory ───────────────────────────────────────────────────────────

    public static Item Create(
        Guid tenantId,
        string sku,
        string shortName,
        string description,
        Guid itemTypeId,
        string defaultUomCode,
        ItemTaxConfig taxConfig,
        ItemSaleConfig saleConfig,
        ItemStockConfig stockConfig,
        Guid createdBy,
        Guid? categoryNodeId = null,
        Guid? brandId = null,
        string? observations = null,
        decimal? baseSalePrice = null)
    {
        if (string.IsNullOrWhiteSpace(defaultUomCode))
            throw new ArgumentException("La unidad de medida base es obligatoria.", nameof(defaultUomCode));
        if (itemTypeId == Guid.Empty)
            throw new ArgumentException("El tipo de ítem es obligatorio.", nameof(itemTypeId));
        if (baseSalePrice.HasValue && baseSalePrice.Value < 0)
            throw new ArgumentException("El precio base no puede ser negativo.", nameof(baseSalePrice));

        var code = ItemCode.Create(sku, shortName, description);

        var item = new Item
        {
            TenantId     = tenantId,
            Code             = code,
            ItemTypeId       = itemTypeId,
            DefaultUomCode   = defaultUomCode.Trim().ToUpperInvariant(),
            TaxConfig        = taxConfig,
            SaleConfig       = saleConfig,
            StockConfig      = stockConfig,
            CategoryNodeId   = categoryNodeId,
            BrandId          = brandId,
            Observations     = observations?.Trim(),
            BaseSalePrice    = baseSalePrice,
        };

        item.SetCreated(createdBy);
        item.RaiseDomainEvent(new ItemCreatedEvent(item.Id, code.SKU, itemTypeId, tenantId));
        return item;
    }

    // ── Actualización ─────────────────────────────────────────────────────

    /// <summary>
    /// Punto único de auditoría "Item actualizado": <c>UpdateItemCommandHandler</c> siempre
    /// invoca este método exactamente una vez por edición completa, junto a los demás
    /// <c>Update*</c> — levantar el evento aquí evita múltiples filas de auditoría para una
    /// sola acción de "Guardar" (ver <see cref="ItemUpdatedEvent"/>).
    /// </summary>
    public void UpdateIdentity(
        string shortName, string description, string? observations, Guid updatedBy)
    {
        Code         = ItemCode.Create(Code.SKU, shortName, description);
        Observations = observations?.Trim();
        SetUpdated(updatedBy);
        RaiseDomainEvent(new ItemUpdatedEvent(Id, TenantId));
    }

    /// <summary>
    /// El SKU es un atributo de negocio único pero editable — el identificador
    /// relacional real del agregado es Id (Guid), del cual dependen todas las
    /// relaciones (variantes, precios, barcodes, líneas de documentos). Editar
    /// el SKU no afecta el SKU ya asignado de variantes existentes (independiente).
    /// </summary>
    public void UpdateSku(string sku, Guid updatedBy)
    {
        Code = ItemCode.Create(sku, Code.ShortName, Code.Description);
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

    public void UpdateBaseSalePrice(decimal? baseSalePrice, Guid updatedBy)
    {
        if (baseSalePrice.HasValue && baseSalePrice.Value < 0)
            throw new ArgumentException("El precio base no puede ser negativo.", nameof(baseSalePrice));

        var oldPrice = BaseSalePrice;
        if (oldPrice == baseSalePrice)
            return; // Sin cambio real — no genera auditoría ni evento.

        BaseSalePrice = baseSalePrice;
        SetUpdated(updatedBy);
        RaiseDomainEvent(new ItemPriceChangedEvent(Id, TenantId, oldPrice, baseSalePrice));
    }

    // ── Variantes ─────────────────────────────────────────────────────────

    public ItemVariant AddVariant(
        IReadOnlyList<(Guid AttributeDefinitionId, string Value)> axisAttributes,
        string? skuOverride,
        int sortOrder,
        Guid updatedBy)
    {
        var variantSku = skuOverride?.Trim()
            ?? (axisAttributes.Count > 0
                ? $"{Code.SKU}-{string.Join("-", axisAttributes.Select(a => a.Value.ToUpperInvariant()))}"
                : Code.SKU);

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
        var variant = ItemVariant.Create(Id, TenantId, variantSku, axisAttributes, isDefault, sortOrder, updatedBy);
        _variants.Add(variant);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new ItemVariantAddedEvent(Id, variant.Id, variant.SKU, TenantId));
        return variant;
    }

    public void DisableVariant(Guid variantId, Guid updatedBy)
    {
        var variant = _variants.FirstOrDefault(v => v.Id == variantId)
            ?? throw new InvalidOperationException("Variante no encontrada.");

        variant.Disable(updatedBy);
        SetUpdated(updatedBy);
        RaiseDomainEvent(new ItemVariantDisabledEvent(Id, variantId, TenantId));
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
            _images.Add(ItemImage.Create(Id, TenantId, i.StorageObjectId, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder, updatedBy));

        SetUpdated(updatedBy);
    }

    // ── Conversiones de UOM ───────────────────────────────────────────────

    public void ReplaceUnitConversions(
        IEnumerable<(string FromUomCode, string ToUomCode, decimal Factor)> conversions,
        Guid updatedBy)
    {
        _unitConversions.Clear();
        foreach (var c in conversions)
            _unitConversions.Add(ItemUnitConversion.Create(Id, TenantId, c.FromUomCode, c.ToUomCode, c.Factor, updatedBy));
        SetUpdated(updatedBy);
    }

    // ── Sustitutos ────────────────────────────────────────────────────────

    public void ReplaceSubstitutes(
        IEnumerable<(Guid SubstituteItemId, int Priority, string? Note)> substitutes,
        Guid updatedBy)
    {
        _substitutes.Clear();
        foreach (var s in substitutes)
            _substitutes.Add(ItemSubstitute.Create(Id, TenantId, s.SubstituteItemId, s.Priority, s.Note, updatedBy));
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
                Id, TenantId,
                l.Name, l.Level, l.BaseQuantity, l.UomCode,
                l.Barcode, l.Weight, l.IsBaseUnit, l.IsPurchaseDefault, l.IsSaleDefault, updatedBy));
        SetUpdated(updatedBy);
    }

    // ── Códigos de proveedor ──────────────────────────────────────────────

    public ItemSupplierCode AddSupplierCode(string code, bool isPrimary, Guid supplierId, Guid updatedBy)
    {
        var supplierCode = ItemSupplierCode.Create(Id, TenantId, supplierId, code, updatedBy, isPrimary);
        _supplierCodes.Add(supplierCode);
        SetUpdated(updatedBy);
        return supplierCode;
    }

    /// <summary>
    /// Desactiva (soft-disable, nunca elimina) un código de proveedor previamente registrado por
    /// una conciliación automática de Item Matching que resultó incorrecta — nunca toca códigos
    /// marcados como primarios, que se asumen curados manualmente por el usuario. No-op si el
    /// código no existe o ya está inactivo.
    /// </summary>
    public void DisableSupplierCode(Guid supplierId, string code, Guid updatedBy)
    {
        var supplierCode = _supplierCodes.FirstOrDefault(sc =>
            sc.SupplierId == supplierId && sc.Code == code.Trim() && sc.IsActive && !sc.IsPrimary);
        if (supplierCode is null)
            return;

        supplierCode.Disable();
        SetUpdated(updatedBy);
    }

    // ── Disable / Enable con invariante de negocio ────────────────────────

    public new void Disable(Guid updatedBy)
    {
        base.Disable(updatedBy);
        RaiseDomainEvent(new ItemDisabledEvent(Id, TenantId));
    }

    public new void Enable(Guid updatedBy)
    {
        base.Enable(updatedBy);
        RaiseDomainEvent(new ItemEnabledEvent(Id, TenantId));
    }
}

using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Variante de ítem (Color/Talla/etc.). Ciclo de vida controlado por el AR Item.
/// Tiene repositorio propio para queries directas desde Inventory y Pricing.
/// </summary>
public sealed class ItemVariant : MasterEntity, ISubscriberScopedEntity
{
    private readonly List<ItemVariantAttribute> _attributes = new();
    private readonly List<ItemVariantBarcode> _barcodes = new();

    public Guid ItemId { get; private set; }
    public string SKU { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsDefault { get; private set; }
    public int SortOrder { get; private set; }

    public IReadOnlyList<ItemVariantAttribute> Attributes => _attributes.AsReadOnly();
    public IReadOnlyList<ItemVariantBarcode> Barcodes => _barcodes.AsReadOnly();

    private ItemVariant() { }

    internal static ItemVariant Create(
        Guid itemId,
        Guid subscriberId,
        string sku,
        IReadOnlyList<(Guid AttributeDefinitionId, string Value)> attributes,
        bool isDefault,
        int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("El SKU de la variante es obligatorio.", nameof(sku));

        var name = string.Join(" / ", attributes.Select(a => a.Value));

        var variant = new ItemVariant
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
            ItemId       = itemId,
            SKU          = sku.Trim().ToUpperInvariant(),
            Name         = name,
            IsDefault    = isDefault,
            SortOrder    = sortOrder,
        };

        foreach (var attr in attributes)
            variant._attributes.Add(
                ItemVariantAttribute.Create(variant.Id, subscriberId, attr.AttributeDefinitionId, attr.Value));

        return variant;
    }

    public void AddBarcode(string code, Enums.BarcodeType type, Guid subscriberId)
    {
        if (_barcodes.Any(b => b.IsActive && b.Code == code))
            throw new InvalidOperationException($"El código de barras '{code}' ya existe en esta variante.");

        _barcodes.Add(ItemVariantBarcode.Create(ItemId, subscriberId, code, type, Id));
    }

    public void SetAsDefault()  => IsDefault = true;
    public void ClearDefault()  => IsDefault = false;
    public void UpdateSortOrder(int order) => SortOrder = order;
}

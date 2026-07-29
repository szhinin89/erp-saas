using ERP.Domain.Common;

namespace ERP.Domain.Modules.Items.Entities;

/// <summary>
/// Variante de ítem (Color/Talla/etc.). Ciclo de vida controlado por el AR Item.
/// Tiene repositorio propio para queries directas desde Inventory y Pricing.
/// </summary>
public sealed class ItemVariant : MasterEntity, ITenantScopedEntity
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
        Guid tenantId,
        string sku,
        IReadOnlyList<(Guid AttributeDefinitionId, string Value)> attributes,
        bool isDefault,
        int sortOrder,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("El SKU de la variante es obligatorio.", nameof(sku));

        var name = string.Join(" / ", attributes.Select(a => a.Value));

        var variant = new ItemVariant
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ItemId = itemId,
            SKU = sku.Trim().ToUpperInvariant(),
            Name = name,
            IsDefault = isDefault,
            SortOrder = sortOrder,
        };
        variant.SetCreated(createdBy);

        foreach (var attr in attributes)
            variant._attributes.Add(
                ItemVariantAttribute.Create(
                    variant.Id,
                    tenantId,
                    attr.AttributeDefinitionId,
                    attr.Value,
                    createdBy
                )
            );

        return variant;
    }

    public void AddBarcode(
        string code,
        string type,
        Guid tenantId,
        Guid createdBy,
        bool isPrimary = false
    )
    {
        if (_barcodes.Any(b => b.IsActive && b.Code == code))
            throw new InvalidOperationException(
                $"El código de barras '{code}' ya existe en esta variante."
            );

        _barcodes.Add(
            ItemVariantBarcode.Create(ItemId, tenantId, code, type, createdBy, Id, isPrimary)
        );
    }

    public void DisableBarcode(Guid barcodeId, Guid updatedBy)
    {
        var bc = _barcodes.FirstOrDefault(b => b.Id == barcodeId);
        if (bc is null)
            throw new InvalidOperationException("Código de barras no encontrado en esta variante.");
        bc.Disable(updatedBy);
    }

    public void SetAsDefault() => IsDefault = true;

    public void ClearDefault() => IsDefault = false;

    public void UpdateSortOrder(int order) => SortOrder = order;
}

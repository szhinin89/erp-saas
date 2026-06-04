using ERP.Domain.Common;

namespace ERP.Domain.Modules.SupplierCatalog.Entities;

/// <summary>
/// Aggregate Root. Ítem del catálogo de proveedor.
/// Scope: subscriber — compartido entre companies.
/// Encapsula: código del proveedor, nombre, MOQ, lead time, tarifas por cantidad.
/// </summary>
public sealed class SupplierItem : MasterEntity, ISubscriberScopedEntity
{
    private readonly List<SupplierItemPrice> _prices = new();

    public Guid SupplierId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string SupplierCode { get; private set; } = null!;
    public string? SupplierName { get; private set; }
    public string DefaultUomCode { get; private set; } = null!;
    public int LeadTimeDays { get; private set; }
    public decimal MinOrderQty { get; private set; }
    public bool IsDefault { get; private set; }

    public IReadOnlyList<SupplierItemPrice> Prices => _prices.AsReadOnly();

    private SupplierItem() { }

    public static SupplierItem Create(
        Guid subscriberId,
        Guid supplierId,
        Guid itemId,
        string supplierCode,
        string defaultUomCode,
        Guid createdBy,
        Guid? variantId = null,
        string? supplierName = null,
        int leadTimeDays = 0,
        decimal minOrderQty = 1,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(supplierCode))
            throw new ArgumentException("El código de proveedor es obligatorio.", nameof(supplierCode));
        if (string.IsNullOrWhiteSpace(defaultUomCode))
            throw new ArgumentException("La unidad de medida es obligatoria.", nameof(defaultUomCode));
        if (leadTimeDays < 0)
            throw new ArgumentException("El tiempo de entrega no puede ser negativo.", nameof(leadTimeDays));
        if (minOrderQty <= 0)
            throw new ArgumentException("La cantidad mínima de orden debe ser mayor que cero.", nameof(minOrderQty));

        var si = new SupplierItem
        {
            SubscriberId   = subscriberId,
            SupplierId     = supplierId,
            ItemId         = itemId,
            VariantId      = variantId,
            SupplierCode   = supplierCode.Trim(),
            SupplierName   = supplierName?.Trim(),
            DefaultUomCode = defaultUomCode.Trim().ToUpperInvariant(),
            LeadTimeDays   = leadTimeDays,
            MinOrderQty    = minOrderQty,
            IsDefault      = isDefault,
        };
        si.SetCreated(createdBy);
        return si;
    }

    public void Update(
        string supplierCode, string? supplierName,
        string defaultUomCode, int leadTimeDays, decimal minOrderQty,
        Guid updatedBy)
    {
        if (minOrderQty <= 0)
            throw new ArgumentException("La cantidad mínima debe ser mayor que cero.", nameof(minOrderQty));

        SupplierCode   = supplierCode.Trim();
        SupplierName   = supplierName?.Trim();
        DefaultUomCode = defaultUomCode.Trim().ToUpperInvariant();
        LeadTimeDays   = leadTimeDays;
        MinOrderQty    = minOrderQty;
        SetUpdated(updatedBy);
    }

    public void SetAsDefault(Guid updatedBy)
    {
        IsDefault = true;
        SetUpdated(updatedBy);
    }

    public SupplierItemPrice AddPrice(
        decimal minQty, decimal unitPrice, string currencyCode,
        Guid updatedBy,
        DateOnly? validFrom = null, DateOnly? validTo = null)
    {
        var price = SupplierItemPrice.Create(
            Id, SubscriberId, minQty, unitPrice, currencyCode, validFrom, validTo);
        _prices.Add(price);
        SetUpdated(updatedBy);
        return price;
    }

    public void DisablePrice(Guid priceId, Guid updatedBy)
    {
        var price = _prices.FirstOrDefault(p => p.Id == priceId)
            ?? throw new InvalidOperationException("Precio de proveedor no encontrado.");
        price.Disable();
        SetUpdated(updatedBy);
    }
}

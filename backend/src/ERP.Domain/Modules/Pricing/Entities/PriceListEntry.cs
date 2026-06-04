using ERP.Domain.Common;

namespace ERP.Domain.Modules.Pricing.Entities;

public sealed class PriceListEntry : BaseEntity
{
    public Guid PriceListId { get; private set; }
    public Guid ItemId { get; private set; }
    public Guid? VariantId { get; private set; }
    public string UomCode { get; private set; } = null!;
    public decimal Price { get; private set; }
    public decimal MinQty { get; private set; }
    public bool IsActive { get; private set; }

    private PriceListEntry() { }

    internal static PriceListEntry Create(
        Guid priceListId, Guid subscriberId,
        Guid itemId, Guid? variantId, string uomCode,
        decimal price, decimal minQty)
    {
        if (price < 0)
            throw new ArgumentException("El precio no puede ser negativo.", nameof(price));
        if (minQty <= 0)
            throw new ArgumentException("La cantidad mínima debe ser mayor que cero.", nameof(minQty));
        if (string.IsNullOrWhiteSpace(uomCode))
            throw new ArgumentException("El código UOM es obligatorio.", nameof(uomCode));

        return new PriceListEntry
        {
            Id          = Guid.NewGuid(),
            SubscriberId = subscriberId,
            PriceListId = priceListId,
            ItemId      = itemId,
            VariantId   = variantId,
            UomCode     = uomCode.Trim().ToUpperInvariant(),
            Price       = price,
            MinQty      = minQty,
            IsActive    = true,
        };
    }

    internal void UpdatePrice(decimal price) => Price = price;
    public void Disable() => IsActive = false;
    public void Enable()  => IsActive = true;
}

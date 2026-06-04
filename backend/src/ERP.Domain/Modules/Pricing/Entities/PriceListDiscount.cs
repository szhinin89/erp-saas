using ERP.Domain.Common;
using ERP.Domain.Modules.Pricing.Enums;

namespace ERP.Domain.Modules.Pricing.Entities;

public sealed class PriceListDiscount : BaseEntity
{
    public Guid PriceListId { get; private set; }
    public Guid? ItemId { get; private set; }
    public Guid? CategoryNodeId { get; private set; }
    public DiscountType DiscountType { get; private set; }
    public decimal DiscountValue { get; private set; }
    public decimal? MinQty { get; private set; }
    public DateOnly? ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public bool IsActive { get; private set; }

    private PriceListDiscount() { }

    internal static PriceListDiscount Create(
        Guid priceListId, Guid subscriberId,
        DiscountType discountType, decimal discountValue,
        Guid? itemId = null, Guid? categoryNodeId = null,
        decimal? minQty = null,
        DateOnly? validFrom = null, DateOnly? validTo = null)
    {
        if (discountValue <= 0)
            throw new ArgumentException("El valor del descuento debe ser mayor que cero.", nameof(discountValue));
        if (discountType == DiscountType.Percentage && discountValue > 100)
            throw new ArgumentException("El porcentaje de descuento no puede superar 100.", nameof(discountValue));
        if (validTo.HasValue && validFrom.HasValue && validTo < validFrom)
            throw new ArgumentException("La fecha de fin no puede ser anterior a la de inicio.");

        return new PriceListDiscount
        {
            Id             = Guid.NewGuid(),
            SubscriberId   = subscriberId,
            PriceListId    = priceListId,
            ItemId         = itemId,
            CategoryNodeId = categoryNodeId,
            DiscountType   = discountType,
            DiscountValue  = discountValue,
            MinQty         = minQty,
            ValidFrom      = validFrom,
            ValidTo        = validTo,
            IsActive       = true,
        };
    }

    public void Disable() => IsActive = false;
}

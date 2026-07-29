namespace ERP.Domain.Modules.Items.ValueObjects;

public sealed class ItemSaleConfig
{
    public bool IsForSale { get; private set; }
    public decimal? MaxDiscountPercent { get; private set; }
    public bool IsAvailableOnWeb { get; private set; }
    public bool IsAvailableOnPOS { get; private set; }
    public bool IsAvailableOnMobile { get; private set; }
    public bool IsEcommerceActive { get; private set; }
    public bool IsFavorite { get; private set; }

    private ItemSaleConfig() { }

    public static ItemSaleConfig Create(
        bool isForSale = true,
        decimal? maxDiscountPercent = null,
        bool isAvailableOnWeb = false,
        bool isAvailableOnPOS = false,
        bool isAvailableOnMobile = false,
        bool isEcommerceActive = false,
        bool isFavorite = false
    )
    {
        if (maxDiscountPercent is < 0 or > 100)
            throw new ArgumentException(
                "El descuento máximo debe estar entre 0 y 100.",
                nameof(maxDiscountPercent)
            );

        return new ItemSaleConfig
        {
            IsForSale = isForSale,
            MaxDiscountPercent = maxDiscountPercent,
            IsAvailableOnWeb = isAvailableOnWeb,
            IsAvailableOnPOS = isAvailableOnPOS,
            IsAvailableOnMobile = isAvailableOnMobile,
            IsEcommerceActive = isEcommerceActive,
            IsFavorite = isFavorite,
        };
    }
}

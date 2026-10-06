namespace ERP.Domain.Modules.Items.ValueObjects;

public sealed class ItemStockConfig
{
    public bool StockControlEnabled { get; private set; }
    public bool TracksLot { get; private set; }
    public bool TracksSeries { get; private set; }
    public bool AllowDecimalQty { get; private set; }
    public bool AllowDecimalSale { get; private set; }
    public decimal? MinStockQty { get; private set; }
    public decimal? MaxStockQty { get; private set; }

    private ItemStockConfig() { }

    public static ItemStockConfig Create(
        bool stockControlEnabled = true,
        bool tracksLot = false,
        bool tracksSeries = false,
        bool allowDecimalQty = false,
        bool allowDecimalSale = false,
        decimal? minStockQty = null,
        decimal? maxStockQty = null
    )
    {
        if (minStockQty.HasValue && minStockQty < 0)
            throw new ArgumentException(
                "El stock mínimo no puede ser negativo.",
                nameof(minStockQty)
            );
        if (maxStockQty.HasValue && maxStockQty < 0)
            throw new ArgumentException(
                "El stock máximo no puede ser negativo.",
                nameof(maxStockQty)
            );
        if (minStockQty.HasValue && maxStockQty.HasValue && minStockQty > maxStockQty)
            throw new ArgumentException(
                "El stock mínimo no puede ser mayor que el máximo.",
                nameof(minStockQty)
            );

        return new ItemStockConfig
        {
            StockControlEnabled = stockControlEnabled,
            TracksLot = tracksLot,
            TracksSeries = tracksSeries,
            AllowDecimalQty = allowDecimalQty,
            AllowDecimalSale = allowDecimalSale,
            MinStockQty = minStockQty,
            MaxStockQty = maxStockQty,
        };
    }
}

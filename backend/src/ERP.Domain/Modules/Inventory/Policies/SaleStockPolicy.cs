namespace ERP.Domain.Modules.Inventory.Policies;

/// <summary>Availability policy only. Products always generate inventory movements.</summary>
public static class SaleStockPolicy
{
    public static bool RequiresAvailableStock(
        bool participatesInInventory,
        bool companyStockControl,
        bool itemStockControl,
        bool allowSellWithoutStock
    ) => participatesInInventory && companyStockControl && itemStockControl && !allowSellWithoutStock;
}

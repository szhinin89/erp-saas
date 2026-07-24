namespace ERP.Domain.Modules.Inventory.Enums;

public enum StockMovementType
{
    PurchaseEntry     = 1,
    SaleExit          = 2,
    PositiveAdjust    = 3,
    NegativeAdjust    = 4,
    TransferEntry     = 5,
    TransferExit      = 6,
    PurchaseReturn    = 7,
    SaleReturn        = 8,
    SupplierCreditNote = 9,
    SupplierDebitNote  = 10,
}

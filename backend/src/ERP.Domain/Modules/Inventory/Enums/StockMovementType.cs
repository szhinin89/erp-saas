namespace ERP.Domain.Modules.Inventory.Enums;

public enum StockMovementType
{
    PurchaseEntry = 1,
    SaleExit = 2,
    PositiveAdjust = 3,
    NegativeAdjust = 4,
    TransferEntry = 5,
    TransferExit = 6,
    PurchaseReturn = 7,
    SaleReturn = 8,
    SupplierCreditNote = 9,
    SupplierDebitNote = 10,

    // PURCHASE-CANCEL-KARDEX-MOVEMENT-IDENTIFIER-01 — reversa de stock por anulación de
    // PurchaseInvoice (CancelPurchaseHandler) tenía su propio hecho de negocio (nunca fue una
    // devolución real a proveedor) pero reutilizaba PurchaseReturn, dejando ambos casos
    // indistinguibles salvo por SourceDocType — corregido en la fuente, no con un label de
    // frontend. Aditivo puro: no reasigna ningún valor existente, persistido como int
    // (HasConversion<int>() en StockMovementConfiguration), sin CHECK constraint que ampliar.
    PurchaseCancelled = 11,
}

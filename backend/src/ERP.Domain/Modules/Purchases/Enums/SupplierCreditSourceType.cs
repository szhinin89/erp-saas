namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — origen de un <see cref="Entities.SupplierCredit"/>.
/// Flag interno NO administrable y NUNCA persistido: se deriva de cuál FK de origen está informada
/// (<c>SourcePurchaseReturnId</c> / <c>SourceSupplierPaymentId</c>, exactamente una).
/// </summary>
public enum SupplierCreditSourceType
{
    PurchaseReturn = 1,
    SupplierPayment = 2,
}

namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// Tipo de movimiento de <see cref="Entities.SupplierCreditMovement"/> — única colección que
/// determina <see cref="Entities.SupplierCredit.AvailableAmount"/> (fórmula completa en diseño
/// P0-02 §13.5). <see cref="SourceReturnCancelled"/> es de sistema, nunca seleccionable por el
/// usuario en la API pública de aplicación/reembolso — se genera exclusivamente como efecto
/// atómico de <see cref="Entities.PurchaseReturn"/>.Cancel() cuando existe un crédito íntegro
/// asociado (§9.3). <see cref="SourcePaymentReversed"/> (ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C)
/// es su equivalente para un crédito originado por un <c>SupplierPayment</c>: efecto atómico de
/// reversar ese pago mientras el anticipo sigue íntegro — también de sistema, nunca seleccionable.
/// </summary>
public enum SupplierCreditMovementType
{
    Application = 1,
    Refund = 2,
    ReversalOfApplication = 3,
    ReversalOfRefund = 4,
    SourceReturnCancelled = 5,
    SourcePaymentReversed = 6,
}

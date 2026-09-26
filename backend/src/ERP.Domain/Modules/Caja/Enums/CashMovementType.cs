namespace ERP.Domain.Modules.Caja.Enums;

public enum CashMovementType
{
    Opening = 1,
    SaleIncome = 2,
    ManualIncome = 3,
    ManualExpense = 4,
    Withdrawal = 5,

    /// <summary>Reembolso en efectivo de una devolución de venta (P0-01, Fase 6).</summary>
    SaleRefund = 6,

    /// <summary>
    /// Egreso de efectivo por una línea de pago a proveedor (<c>SupplierPaymentMethodLine</c>) —
    /// ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A. Efecto operativo únicamente: el asiento lo
    /// genera <c>SupplierPayment</c>, nunca este movimiento.
    /// </summary>
    SupplierPayment = 7,

    /// <summary>Ingreso compensatorio por la reversa de un <see cref="SupplierPayment"/> (el original nunca se borra).</summary>
    SupplierPaymentReversal = 8,
}

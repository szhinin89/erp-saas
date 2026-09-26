namespace ERP.Domain.Modules.Caja.Enums;

public enum CashReferenceType
{
    None = 0,
    SalesInvoice = 1,

    /// <summary>Reembolso en efectivo de una devolución de venta (P0-01, Fase 6).</summary>
    SalesReturn = 2,

    /// <summary>Pago a proveedor (<c>SupplierPayment</c>) — ReferenceId = SupplierPayment.Id (02A).</summary>
    SupplierPayment = 3,

    /// <summary>
    /// Reembolso de crédito de proveedor en efectivo (<c>SupplierCreditRefundTransaction</c>) —
    /// ReferenceId = Id de la transacción de reembolso ORIGINAL, tanto en el ingreso del reembolso
    /// como en el egreso compensatorio de su reversa (02A-CLOSE).
    /// </summary>
    SupplierCreditRefund = 4,
}

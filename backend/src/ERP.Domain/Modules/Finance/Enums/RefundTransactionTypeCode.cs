namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// Catálogo cerrado (2 valores) de <see cref="Entities.SupplierCreditRefundTransaction"/> — diseño
/// P0-02 §6.4. El estado económico (activo/revertido) se deriva de la existencia o no de una fila
/// <see cref="RefundReversed"/> con <c>OriginalTransactionId</c> apuntando a la transacción original,
/// nunca de un campo editable duplicado.
/// </summary>
public enum RefundTransactionTypeCode
{
    RefundReceived = 1,
    RefundReversed = 2,
}

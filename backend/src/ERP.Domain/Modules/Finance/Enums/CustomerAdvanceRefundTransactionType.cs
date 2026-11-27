namespace ERP.Domain.Modules.Finance.Enums;

/// <summary>
/// ADR-052 §4 — tipo de <see cref="Entities.CustomerAdvanceRefundTransaction"/>. Que una devolución
/// esté revertida se deriva de la existencia de su fila <see cref="RefundReversed"/>, nunca de un
/// campo editable.
/// </summary>
public enum CustomerAdvanceRefundTransactionType
{
    Refund = 1,
    RefundReversed = 2,
}

namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// Máquina de estados de <see cref="Entities.PurchaseCreditNote"/> — diseño FLOW-READY-02C §1bis/§4.
/// <c>Draft → Authorized</c> y <c>Draft → Cancelled</c> (sin reversas) y
/// <c>Authorized → Cancelled</c> (con reversa de <c>PurchasePayable.CreditNoteAppliedAmount</c>).
/// <c>Cancelled</c> es terminal. Mismo patrón que <see cref="PurchaseReturnStatus"/>.
/// </summary>
public enum PurchaseCreditNoteStatus
{
    Draft = 1,
    Authorized = 2,
    Cancelled = 3,
}

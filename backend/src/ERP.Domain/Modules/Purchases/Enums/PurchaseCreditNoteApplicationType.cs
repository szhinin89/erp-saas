namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// FLOW-READY-02C-R1.1 — cómo se aplica una <see cref="Entities.PurchaseCreditNote"/> (cabecera
/// fiscal única de toda nota de crédito de compra) contra la factura afectada. <c>Return</c> delega
/// el movimiento de inventario/CxP/SupplierCredit por completo en <see cref="Entities.PurchaseReturn"/>
/// (vía <see cref="Entities.PurchaseCreditNote.LinkPurchaseReturn"/>) — nunca se autoriza a través de
/// <see cref="Entities.PurchaseCreditNote.Authorize"/>. <c>Discount</c> conserva el flujo original
/// FLOW-READY-02C (autoriza/cancela contra <c>PurchasePayable.CreditNoteAppliedAmount</c>, sin
/// inventario ni contabilidad).
/// </summary>
public enum PurchaseCreditNoteApplicationType
{
    Return = 1,
    Discount = 2,
}

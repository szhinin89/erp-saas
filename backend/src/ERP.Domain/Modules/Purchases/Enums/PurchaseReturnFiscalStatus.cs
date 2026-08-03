namespace ERP.Domain.Modules.Purchases.Enums;

/// <summary>
/// Estado fiscal de <see cref="Entities.PurchaseReturn"/>, independiente del estado operativo
/// (<see cref="PurchaseReturnStatus"/>) — ver diseño P0-02 §9.2. Siempre tiene un valor explícito,
/// nunca <c>null</c>: <see cref="NotApplicable"/> mientras el documento es <c>Draft</c> (todavía no
/// existe efecto fiscal), <see cref="PendingSupplierCreditNote"/> desde <c>Authorize()</c> hasta que
/// se vincula la Nota de Crédito del proveedor, <see cref="SupplierCreditNoteRegistered"/> es
/// terminal (no se desvincula ni retrocede).
/// </summary>
public enum PurchaseReturnFiscalStatus
{
    NotApplicable = 0,
    PendingSupplierCreditNote = 1,
    SupplierCreditNoteRegistered = 2,
}

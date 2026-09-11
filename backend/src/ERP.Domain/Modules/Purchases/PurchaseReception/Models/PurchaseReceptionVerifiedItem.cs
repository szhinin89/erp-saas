using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>Un <see cref="PurchaseReceptionRecord"/> ya cruzado contra proveedores y compras del ERP.</summary>
public sealed record PurchaseReceptionVerifiedItem(
    PurchaseReceptionRecord Record,
    bool SupplierExists,
    bool PurchaseExists,
    PurchaseReceptionStatus Status,
    Guid? SupplierId = null,
    Guid? PurchaseId = null,
    // Solo se resuelve para notas de crédito (Record.SourceDocType == CreditNote): si la factura
    // que la NC afecta (Record.ModifiedDocumentNumber) ya está ingresada como PurchaseInvoice del
    // mismo proveedor. False/null para cualquier otro tipo de comprobante.
    bool AffectedPurchaseExists = false,
    Guid? AffectedPurchaseId = null,
    bool? SupplierIsActive = null,
    // EXPENSES-FROM-RECEPTION-01 — si ya existe un ExpenseDocument ACTIVO (Draft/Confirmed) con
    // esta clave de acceso SRI (bloquea "Crear compra", mirror de PurchaseExists bloqueando "Crear
    // gasto"). RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01: un ExpenseDocument Cancelled nunca
    // cuenta aquí.
    bool ExpenseExists = false,
    // RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01 — Id de la compra/gasto Cancelled más reciente
    // con este AccessKey, solo resuelto cuando NO hay una activa — para "Ver compra/gasto anulado"
    // (historial) en la UI de Recepción.
    Guid? CancelledPurchaseId = null,
    Guid? CancelledExpenseId = null
);

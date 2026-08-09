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
    Guid? AffectedPurchaseId = null
);

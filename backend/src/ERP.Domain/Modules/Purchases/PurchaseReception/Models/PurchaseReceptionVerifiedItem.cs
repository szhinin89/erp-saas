using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>Un <see cref="PurchaseReceptionRecord"/> ya cruzado contra proveedores y compras del ERP.</summary>
public sealed record PurchaseReceptionVerifiedItem(
    PurchaseReceptionRecord Record,
    bool SupplierExists,
    bool PurchaseExists,
    PurchaseReceptionStatus Status,
    Guid? SupplierId = null,
    Guid? PurchaseId = null);

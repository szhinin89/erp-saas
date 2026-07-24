namespace ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

/// <summary>
/// Una línea del borrador — <see cref="ItemId"/>/<see cref="WarehouseId"/> siempre null en esta fase
/// (sin matcher de ítems, el usuario los asocia manualmente en el formulario). Los campos desde
/// <see cref="SupplierCode"/> en adelante son de solo lectura: exactamente lo que trae el XML del
/// comprobante, para que el usuario vea el detalle completo antes de emparejar el producto.
/// </summary>
public sealed record PurchaseDraftLineDto(
    Guid? ItemId, string Description, decimal Quantity, decimal UnitPrice,
    string VatCode, Guid? WarehouseId, string? Notes, decimal DiscountPct, string? IceCode,
    string SupplierCode, string? SupplierAuxCode, decimal Discount, decimal LineSubtotal,
    string TaxCode, decimal VatPercentage, decimal TaxValue, decimal TotalLine);

/// <summary>
/// Respuesta de <c>POST /api/v1/purchases/reception/{id}/create-draft</c> — precarga del formulario
/// de Nueva Compra. <see cref="SupplierId"/> es null cuando el proveedor no fue resuelto al importar
/// (el usuario debe seleccionarlo manualmente).
/// </summary>
public sealed record PurchaseDraftDto(
    Guid? SupplierId, string SupplierRuc, string SupplierName,
    string DocTypeCode, string InvoiceNumber, DateOnly IssueDate,
    string? AccessKey, string? AuthorizationNumber, DateTime? AuthorizationDate,
    string? SriPaymentMethodCode,
    List<PurchaseDraftLineDto> Lines);

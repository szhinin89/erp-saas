namespace ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

/// <summary>
/// Una línea del borrador — proviene 1:1 de la <c>PurchaseReceptionLine</c> ya persistida y
/// conciliada (Item Matching). <see cref="ItemId"/>/<see cref="ItemMatchStatus"/> reflejan el
/// estado real de conciliación (mismo código de estado que <c>ItemMatchingMapper.ToStatusCode</c>:
/// PENDING/NEEDS_REVIEW/AUTO_MATCHED/MANUALLY_MATCHED); <see cref="WarehouseId"/>/<see cref="Notes"/>
/// siguen null porque no existen en la línea de recepción — el usuario los completa manualmente.
/// </summary>
public sealed record PurchaseDraftLineDto(
    Guid PurchaseReceptionLineId,
    Guid? ItemId,
    string ItemMatchStatus,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    string VatCode,
    Guid? WarehouseId,
    string? Notes,
    decimal DiscountPct,
    string? IceCode,
    string? SupplierCode,
    string? SupplierAuxCode,
    decimal Discount,
    decimal LineSubtotal,
    string TaxCode,
    decimal VatPercentage,
    decimal TaxValue,
    decimal TotalLine,
    // FLOW-READY-02F.1 — snapshot fiel de todo impuesto del XML (IVA/ICE/IRBPNR), para que la vista
    // previa "Producto recibido (XML)" del formulario de compra pueda mostrarlos sin adivinar.
    List<PurchaseDraftLineTaxDto> Taxes
);

/// <summary>FLOW-READY-02F.1 — un impuesto crudo del XML, tal como quedó en <c>PurchaseReceptionLineTax</c>.</summary>
public sealed record PurchaseDraftLineTaxDto(
    string TaxCode,
    string TaxRateCode,
    decimal Tarifa,
    decimal TaxableBase,
    decimal TaxAmount
);

/// <summary>
/// Respuesta de <c>POST /api/v1/purchases/reception/{id}/create-draft</c> — precarga del formulario
/// de Nueva Compra. <see cref="SupplierId"/> es null cuando el proveedor no fue resuelto al importar
/// (el usuario debe seleccionarlo manualmente). <see cref="DocTypeCode"/> es null si el XML se
/// autorizó pero no pudo parsearse (comprobante sin detalle interpretable).
/// </summary>
public sealed record PurchaseDraftDto(
    Guid? SupplierId,
    string SupplierRuc,
    string SupplierName,
    string? DocTypeCode,
    string InvoiceNumber,
    DateOnly IssueDate,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    string? SriPaymentMethodCode,
    List<PurchaseDraftLineDto> Lines,
    string ProcessingStatus,
    string? ProcessingNotes
);

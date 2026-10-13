using ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

namespace ERP.Application.Modules.Purchases.DTOs;

public sealed record PurchaseInvoiceDto(
    Guid Id,
    Guid SupplierId,
    string SupplierName,
    string SupplierTaxId,
    string DocTypeCode,
    string InvoiceNumber,
    DateOnly IssueDate,
    string? AccessKey,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    string? TaxSupportCode,
    string? SriPaymentMethodCode,
    string? SriPaymentMethodName,
    string CurrencyCode,
    decimal ExchangeRate,
    Guid? PurchaseOrderId,
    string? PurchaseOrderNumber,
    Guid? GlobalWarehouseId,
    Guid PaymentTermId,
    string PaymentTermName,
    int PaymentTermInstallments,
    int PaymentTermDaysBetween,
    int CreditTermDays,
    DateOnly? DueDate,
    string? Notes,
    string Status,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalIce,
    decimal TotalVat,
    decimal TotalTax,
    decimal TotalFreight,
    decimal TotalOtherCosts,
    decimal GrandTotal,
    // FLOW-READY-02F.1 — informativo, NUNCA incluido en GrandTotal (ver ConfirmPurchaseHandler,
    // guard que bloquea Confirm mientras no exista soporte contable verificado para IRBPNR).
    decimal TotalIrbpnr,
    IReadOnlyList<PurchaseInvoiceDetailDto> Lines,
    IReadOnlyList<PurchasePaymentScheduleDto> PaymentSchedules,
    string? CancelReason,
    DateTime? CancelledAt,
    Guid? CancelledBy,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // PURCHASE-DRAFT-RECOVERY F3 — decisión de retención recordada por el borrador (null = nunca
    // registrada). Solo restaura la UI; la emisión sigue viajando en el body de Confirm.
    PurchaseRetentionIntentDraftDto? RetentionIntent = null,
    // FASE 5 PURCHASE-UX — Cancelled sin haber sido confirmada nunca: "Descartado", no "Anulada".
    bool IsDiscardedDraft = false
);

/// <summary>
/// PURCHASE-DRAFT-RECOVERY F3 — intención de retención de un borrador de compra (entrada en
/// Create/Update y salida en el DTO). Nunca incluye líneas ni montos: la propuesta se recalcula
/// siempre desde la vista previa del backend.
/// </summary>
public sealed record PurchaseRetentionIntentDraftDto(
    bool AppliesRetention,
    Guid? EmissionPointId,
    DateOnly? IssueDate
);

public sealed record PurchasePaymentScheduleDto(
    Guid Id,
    int InstallmentNumber,
    DateOnly DueDate,
    decimal Amount,
    string? Notes
);

public sealed record PurchaseInvoiceDetailDto(
    Guid Id,
    Guid? ItemId,
    string Description,
    // ── Product Snapshot ────────────────────────────────────────────
    string? SnapshotSku,
    string? SnapshotItemName,
    string? SnapshotSupplierCode,
    // ── UoM ─────────────────────────────────────────────────────────
    Guid? PackagingLevelId,
    string UomCode,
    string BaseUomCode,
    decimal ConversionFactor,
    decimal QuantityInBaseUom,
    // ── Quantity & Price ────────────────────────────────────────────
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal DiscountAmount,
    // ── Costs ───────────────────────────────────────────────────────
    decimal FreightAllocated,
    decimal OtherCostsAllocated,
    decimal TotalLineCost,
    decimal LandedUnitCost,
    decimal TaxableBase,
    // ── VAT ─────────────────────────────────────────────────────────
    string VatCode,
    decimal VatRate,
    decimal VatAmount,
    string? SnapshotVatName,
    // ── ICE ─────────────────────────────────────────────────────────
    string? IceCode,
    decimal IceRate,
    decimal IceAmount,
    string? SnapshotIceName,
    // ── IRBPNR (FLOW-READY-02F.1 — sin campos escalares legacy: derivado de Taxes) ───
    string? IrbpnrCode,
    decimal? IrbpnrRate,
    decimal IrbpnrAmount,
    string? SnapshotIrbpnrName,
    // ── Detalle fiel de impuestos del XML (IVA/ICE/IRBPNR, FLOW-READY-02F.1) ─────────
    IReadOnlyList<PurchaseInvoiceDetailTaxDto> Taxes,
    // ── Total ───────────────────────────────────────────────────────
    decimal TaxInclusiveTotal,
    // ── Analytic ────────────────────────────────────────────────────
    decimal SnapshotItemPvp,
    // ── Warehouse ───────────────────────────────────────────────────
    Guid? WarehouseId,
    string? SnapshotWarehouseCode,
    // ── PO Traceability ─────────────────────────────────────────────
    Guid? PurchaseOrderDetailId,
    decimal? OrderedQuantity,
    // ── Purchase Reception Traceability ──────────────────────────────
    Guid? PurchaseReceptionLineId,
    // ── Meta ────────────────────────────────────────────────────────
    string? Notes,
    short SortOrder,
    // PURCHASE-DRAFT-RECOVERY F1 — evidencia original de la PurchaseReceptionLine (mismo contrato
    // que "Crear compra" desde Recepción). Solo se informa al leer un borrador por Id; null en
    // líneas manuales o fuera de ese flujo.
    PurchaseDraftLineDto? XmlSource = null,
    // FASE 5 PURCHASE-UX — presentación de origen XML persistida (null: manual / borrador anterior).
    Guid? XmlOriginPackagingLevelId = null,
    decimal? XmlOriginFactor = null
);

/// <summary>FLOW-READY-02F.1 — snapshot fiel de un &lt;impuesto&gt; del XML (IVA/ICE/IRBPNR).</summary>
public sealed record PurchaseInvoiceDetailTaxDto(
    string TaxCode,
    string TaxRateCode,
    string TaxName,
    decimal? Rate,
    string CalculationType,
    decimal TaxableBase,
    decimal TaxAmount,
    string Source
);

public sealed record PurchaseListDto(
    Guid Id,
    string InvoiceNumber,
    DateOnly IssueDate,
    Guid SupplierId,
    string Status,
    int LineCount,
    DateTime CreatedAt,
    // FASE 5 PURCHASE-UX — datos operativos del listado (snapshot proveedor + total del documento).
    string SupplierName = "",
    string SupplierTaxId = "",
    decimal GrandTotal = 0m,
    // FASE 5 PURCHASE-UX — Cancelled sin haber sido confirmada nunca: "Descartado", no "Anulada".
    bool IsDiscardedDraft = false
);

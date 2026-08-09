namespace ERP.Application.Modules.Purchases.DTOs;

/// <summary>
/// FLOW-READY-02C.2 — proyección de lectura de <c>PurchaseCreditNote</c> (descuento/promoción).
/// Los campos de enriquecimiento (<c>InvoiceNumber</c>/<c>SupplierName</c>/<c>InvoiceBalanceDue</c>/
/// <c>ReceptionDocumentAccessKey</c>) son opcionales: se completan cuando el handler ya tiene esa
/// información cargada (todos los handlers de este módulo cargan la factura para validar, así que
/// en la práctica siempre viajan poblados salvo <c>ReceptionDocumentAccessKey</c> cuando no aplica).
/// </summary>
public sealed record PurchaseCreditNoteDto(
    Guid Id,
    Guid PurchaseInvoiceId,
    Guid SupplierId,
    Guid BranchId,
    Guid? ReceptionDocumentId,
    string ApplicationType,
    Guid? LinkedPurchaseReturnId,
    string Status,
    string CreditNoteNumber,
    string? AccessKey,
    string? AuthorizationNumber,
    DateOnly? AuthorizationDate,
    DateOnly IssueDate,
    string Reason,
    decimal Subtotal,
    decimal IceAmount,
    decimal VatAmount,
    decimal TotalAmount,
    decimal? AppliedToPayableAmount,
    DateTime? AuthorizedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    IReadOnlyList<PurchaseCreditNoteDetailDto> Lines,
    IReadOnlyList<PurchaseCreditNoteTaxSummaryDto> TaxSummaries,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    string? InvoiceNumber = null,
    string? SupplierName = null,
    decimal? InvoiceBalanceDue = null,
    string? ReceptionDocumentAccessKey = null
);

/// <summary>FLOW-READY-02C.2 — proyección de lectura de <c>PurchaseCreditNoteDetail</c> (línea libre, legado).</summary>
public sealed record PurchaseCreditNoteDetailDto(
    Guid Id,
    string Description,
    decimal Subtotal,
    string? VatCode,
    decimal? VatRate,
    decimal VatAmount,
    decimal TotalAmount
);

/// <summary>
/// FLOW-READY-02C-R1.2 — proyección de lectura de <c>PurchaseCreditNoteTaxSummary</c>, flujo
/// principal de descuento/promoción (una línea por grupo de impuesto real de la compra afectada).
/// </summary>
public sealed record PurchaseCreditNoteTaxSummaryDto(
    Guid Id,
    Guid SourcePurchaseInvoiceTaxSummaryId,
    string VatCode,
    decimal VatRate,
    string? VatName,
    string? IceCode,
    decimal IceRate,
    string? IceName,
    decimal TaxableBase,
    decimal IceAmount,
    decimal VatAmount,
    decimal TotalAmount
);

/// <summary>FLOW-READY-02C.2 — proyección liviana para <c>GetPurchaseCreditNoteListQuery</c> (sin líneas).</summary>
public sealed record PurchaseCreditNoteListItemDto(
    Guid Id,
    Guid PurchaseInvoiceId,
    Guid SupplierId,
    string ApplicationType,
    string Status,
    string CreditNoteNumber,
    decimal TotalAmount,
    DateOnly IssueDate,
    DateTime? AuthorizedAtUtc,
    DateTime CreatedAt
);

/// <summary>FLOW-READY-02C.2 — resultado paginado de <c>GetPurchaseCreditNoteListQuery</c>.</summary>
public sealed record PurchaseCreditNoteListResultDto(
    IReadOnlyList<PurchaseCreditNoteListItemDto> Items,
    int Total,
    int Page,
    int PageSize
);

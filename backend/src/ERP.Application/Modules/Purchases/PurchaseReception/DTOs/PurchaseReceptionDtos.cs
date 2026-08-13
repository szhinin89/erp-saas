namespace ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

public sealed record PurchaseReceptionItemDto(
    string SupplierRuc,
    string SupplierName,
    string SourceDocType,
    string InvoiceNumber,
    // Solo aplica a notas de crédito/débito — la factura que modifican. Null en Factura.
    string? ModifiedDocumentNumber,
    string AccessKey,
    DateOnly IssueDate,
    DateTime AuthorizationDate,
    // Subtotal/VatAmount: ya venían en el TXT SRI y se persisten en PurchaseReceptionDocument
    // (Create) desde siempre — solo no se exponían todavía en este DTO.
    decimal Subtotal,
    decimal VatAmount,
    decimal Total,
    bool SupplierExists,
    bool PurchaseExists,
    Guid? PurchaseId,
    // Solo aplica a notas de crédito: si la factura afectada (ModifiedDocumentNumber) ya está
    // ingresada como compra del mismo proveedor. False/null en Factura.
    bool AffectedPurchaseExists,
    Guid? AffectedPurchaseId,
    string Status,
    Guid DocumentId,
    string DocumentStatus,
    string ProcessingStatus,
    string? ProcessingNotes
);

public sealed record PurchaseReceptionImportResultDto(
    IReadOnlyList<PurchaseReceptionItemDto> Items,
    int TotalParsed,
    int ParseErrorCount,
    int SkippedUnsupportedCount
);

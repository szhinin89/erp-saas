namespace ERP.Application.Modules.Purchases.PurchaseReception.DTOs;

public sealed record PurchaseReceptionItemDto(
    string SupplierRuc,
    string SupplierName,
    string InvoiceNumber,
    string AccessKey,
    DateOnly IssueDate,
    DateTime AuthorizationDate,
    decimal Total,
    bool SupplierExists,
    bool PurchaseExists,
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

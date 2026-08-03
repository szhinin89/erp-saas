namespace ERP.Application.Modules.Purchases.DTOs;

/// <summary>P0-02 Fase 5 — proyección de lectura de <c>PurchaseReturn</c> (diseño §7.1, §24).</summary>
public sealed record PurchaseReturnDto(
    Guid Id,
    Guid PurchaseInvoiceId,
    Guid SupplierId,
    Guid BranchId,
    string? ReturnNumber,
    string Reason,
    string Status,
    string FiscalStatus,
    Guid? SupplierCreditNoteDocumentId,
    decimal? AuthorizedSubtotal,
    decimal? AuthorizedVatTotal,
    decimal? AuthorizedIceTotal,
    decimal? AuthorizedDiscountTotal,
    decimal? AuthorizedGrandTotal,
    DateTime? AuthorizedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    IReadOnlyList<PurchaseReturnDetailDto> Lines,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

/// <summary>P0-02 Fase 5 — proyección de lectura de <c>PurchaseReturnDetail</c>.</summary>
public sealed record PurchaseReturnDetailDto(
    Guid Id,
    Guid OriginalInvoiceDetailId,
    Guid ItemId,
    decimal Quantity,
    Guid WarehouseId
);

/// <summary>P0-02 Fase 5 — resultado paginado de <c>GetPurchaseReturnListQuery</c>.</summary>
public sealed record PurchaseReturnListResultDto(
    IReadOnlyList<PurchaseReturnDto> Items,
    int Total,
    int Page,
    int PageSize
);

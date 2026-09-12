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
    DateTime? UpdatedAt,
    // PURCHASE-RETURN-DETAIL-DISPLAY-NAMES-01 — solo poblados por GetPurchaseReturnByIdHandler
    // (la vista de detalle de solo lectura); el resto de handlers (create/update/authorize/cancel)
    // siguen usando Map.ToDto sin tocarlos, así que estos quedan en null ahí — nunca afecta la
    // lógica de autorización/cancelación/reversa, es puramente un dato de presentación adicional
    // resuelto desde PurchaseReceptionDocument (mismo agregado que SupplierCreditNoteDocumentId
    // ya referencia, ver RegisterAndLinkSupplierCreditNoteHandler).
    string? SupplierCreditNoteInvoiceNumber = null,
    string? SupplierCreditNoteAccessKey = null,
    // PURCHASE-RETURN-CREDIT-NOTE-DETAIL-ENRICHMENT-01 — mismo criterio que los dos campos
    // anteriores: solo poblados por GetPurchaseReturnByIdHandler, nunca inventados si no se
    // pueden resolver (quedan en null).
    DateOnly? SupplierCreditNoteIssueDate = null,
    DateTime? SupplierCreditNoteAuthorizationDate = null,
    decimal? SupplierCreditNoteTotalAmount = null,
    string? PurchaseInvoiceNumber = null,
    // Referencia opcional a la PurchaseCreditNote interna (FLOW-READY-02C) vinculada a esta
    // devolución vía LinkPurchaseCreditNoteToReturn — flujo distinto al registro manual de
    // SupplierCreditNoteDocumentId; una devolución puede tener una sin la otra, o ninguna.
    Guid? LinkedPurchaseCreditNoteId = null,
    string? LinkedPurchaseCreditNoteStatus = null
);

/// <summary>P0-02 Fase 5 — proyección de lectura de <c>PurchaseReturnDetail</c>.</summary>
public sealed record PurchaseReturnDetailDto(
    Guid Id,
    Guid OriginalInvoiceDetailId,
    Guid ItemId,
    decimal Quantity,
    Guid WarehouseId,
    // PURCHASE-RETURN-DETAIL-DISPLAY-NAMES-01 — solo poblados por GetPurchaseReturnByIdHandler,
    // ver nota en PurchaseReturnDto. Nunca inventados: si el ítem/bodega no se pudo resolver (p. ej.
    // eliminado), quedan en null y el frontend cae de vuelta al Id crudo.
    string? ItemSku = null,
    string? ItemName = null,
    string? WarehouseName = null
);

/// <summary>P0-02 Fase 5 — resultado paginado de <c>GetPurchaseReturnListQuery</c>.</summary>
public sealed record PurchaseReturnListResultDto(
    IReadOnlyList<PurchaseReturnDto> Items,
    int Total,
    int Page,
    int PageSize
);

namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.BatchDownloadPurchaseReceptionXml;

/// <summary>
/// Resultado por documento de un lote de descarga de XML SRI. <see cref="Status"/> es uno de:
/// Downloaded, SkippedAlreadyHasXml, SriError, ValidationError, Error.
///
/// Los campos desde <see cref="DocumentStatus"/> en adelante son deliberadamente un resumen
/// mínimo (nunca el XML ni las líneas) — solo lo necesario para refrescar los badges de la fila
/// de Recepción sin que el frontend tenga que abrir "Ver XML" por cada documento. Todos quedan
/// `null`/`false` cuando el documento no existe (<see cref="Status"/> = Error).
/// </summary>
public sealed record BatchDownloadPurchaseReceptionXmlItemResult(
    Guid DocumentId,
    string Status,
    string Message,
    string? DocumentStatus = null,
    string? ProcessingStatus = null,
    bool HasXml = false,
    bool PurchaseExists = false,
    Guid? PurchaseId = null,
    bool ExpenseExists = false,
    Guid? ExpenseId = null,
    bool CreditNoteExists = false,
    Guid? CreditNoteId = null,
    Guid? CancelledCreditNoteId = null
);

public sealed record BatchDownloadPurchaseReceptionXmlResultDto(
    int Total,
    int Processed,
    int Downloaded,
    int Skipped,
    int Failed,
    IReadOnlyList<BatchDownloadPurchaseReceptionXmlItemResult> Items
);

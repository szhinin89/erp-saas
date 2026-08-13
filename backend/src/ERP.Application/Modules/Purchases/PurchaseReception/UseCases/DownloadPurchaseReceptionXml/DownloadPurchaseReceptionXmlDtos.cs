namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;

public sealed record DownloadPurchaseReceptionXmlResultDto(
    Guid DocumentId,
    string Status,
    bool XmlDownloaded,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate,
    string ProcessingStatus,
    int LinesDetectedCount,
    int LinesProcessedCount,
    string? ProcessingNotes,
    bool PurchaseExists,
    Guid? PurchaseId,
    bool? SupplierIsActive,
    // infoTributaria/nombreComercial del emisor — solo para precargar el formulario de creación de
    // proveedor cuando el BP todavía no existe en el ERP; null si el XML no lo trae o no se pudo
    // interpretar la cabecera.
    string? SupplierTradeName
);

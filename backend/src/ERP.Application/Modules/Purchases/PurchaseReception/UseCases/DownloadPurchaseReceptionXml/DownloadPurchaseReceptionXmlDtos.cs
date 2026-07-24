namespace ERP.Application.Modules.Purchases.PurchaseReception.UseCases.DownloadPurchaseReceptionXml;

public sealed record DownloadPurchaseReceptionXmlResultDto(
    Guid DocumentId,
    string Status,
    bool XmlDownloaded,
    string? AuthorizationNumber,
    DateTime? AuthorizationDate);

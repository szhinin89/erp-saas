using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Models;

/// <summary>
/// Resultado de intentar interpretar el detalle de un XML SRI al descargarlo — trazabilidad para
/// soporte técnico (ver <c>DownloadPurchaseReceptionXmlHandler</c>). Se pasa como un único valor a
/// <see cref="Entities.PurchaseReceptionDocument.AttachSriAuthorization"/> para no seguir creciendo
/// la firma posicional del método.
/// </summary>
public sealed record PurchaseReceptionProcessingOutcome(
    PurchaseReceptionProcessingStatus Status,
    int LinesDetected,
    int LinesProcessed,
    string? Notes
)
{
    public static PurchaseReceptionProcessingOutcome Failed(string notes) =>
        new(PurchaseReceptionProcessingStatus.Failed, LinesDetected: 0, LinesProcessed: 0, notes);
}

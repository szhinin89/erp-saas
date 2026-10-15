using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Purchases.Interfaces;
using ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

namespace ERP.Domain.Modules.Purchases.PurchaseReception.Services;

/// <summary>
/// FASE 5 PURCHASE-UX — compra relacionada con una recepción. <see cref="PurchaseId"/> = compra
/// ACTIVA (Draft/Confirmed); <see cref="CancelledPurchaseId"/> = última Cancelled/Discarded, solo cuando no hay
/// activa (historial "Ver compra anulada / Ver borrador descartado").
/// </summary>
public sealed record PurchaseReceptionPurchaseInfo(
    PurchaseReceptionPurchaseState State,
    Guid? PurchaseId,
    Guid? CancelledPurchaseId
)
{
    public static readonly PurchaseReceptionPurchaseInfo None = new(
        PurchaseReceptionPurchaseState.None,
        null,
        null
    );
}

/// <summary>
/// FASE 5 PURCHASE-UX — única resolución del estado de la compra de una recepción, reutilizada por la
/// importación del TXT (<c>PurchaseReceptionVerifier</c>) y por la descarga de XML individual y en
/// lote. Mismo criterio de "activa" que el resto del sistema
/// (RECEPTION-REPROCESS-AFTER-CANCEL-STANDARD-01): Draft/Confirmed cuentan, Cancelled/Discarded son historial.
/// A lo sumo dos consultas: la compra activa y, solo si no hay, la última histórica.
/// </summary>
public static class PurchaseReceptionPurchaseStateResolver
{
    public static async Task<PurchaseReceptionPurchaseInfo> ResolveAsync(
        IPurchaseInvoiceRepository purchases,
        Guid tenantId,
        string accessKey,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(accessKey))
            return PurchaseReceptionPurchaseInfo.None;

        var active = await purchases.GetByAccessKeyAsync(tenantId, accessKey, ct);
        if (active is not null)
            return new(
                active.Status == PurchaseStatus.Confirmed
                    ? PurchaseReceptionPurchaseState.Confirmed
                    : PurchaseReceptionPurchaseState.Draft,
                active.Id,
                null
            );

        var historical = await purchases.GetLatestHistoricalByAccessKeyAsync(tenantId, accessKey, ct);
        if (historical is null)
            return PurchaseReceptionPurchaseInfo.None;

        return new(
            historical.Status == PurchaseStatus.Discarded
                ? PurchaseReceptionPurchaseState.Discarded
                : PurchaseReceptionPurchaseState.Cancelled,
            null,
            historical.Id
        );
    }
}

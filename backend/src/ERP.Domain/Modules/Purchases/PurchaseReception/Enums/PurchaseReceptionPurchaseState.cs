namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>
/// FASE 5 PURCHASE-UX — estado REAL de la compra relacionada con una recepción (por clave de acceso),
/// para que Recepción distinga borrador / confirmada / anulada / descartada en vez de un booleano
/// "ya ingresada". Resuelto únicamente por <c>PurchaseReceptionPurchaseStateResolver</c>.
/// </summary>
public enum PurchaseReceptionPurchaseState
{
    /// <summary>Ninguna compra con esta clave de acceso.</summary>
    None = 0,

    /// <summary>Compra activa en borrador.</summary>
    Draft = 1,

    /// <summary>Compra activa confirmada.</summary>
    Confirmed = 2,

    /// <summary>Sin compra activa; la última Cancelled fue confirmada y anulada.</summary>
    Cancelled = 3,

    /// <summary>Sin compra activa; la última Cancelled nunca se confirmó (borrador descartado).</summary>
    Discarded = 4,
}

namespace ERP.Domain.Modules.Ride.Enums;

/// <summary>
/// Tipo de comprobante que el módulo Ride sabe representar como RIDE. Espejo desacoplado de
/// <c>ElectronicDocumentType</c> (ADR-025 §2: Ride nunca conoce <c>ElectronicDocuments</c>) —
/// la traducción entre ambos vocabularios es responsabilidad exclusiva de
/// <c>ElectronicDocumentRideSourceXmlProvider</c> (ADR-025 §8), nunca de este enum.
/// </summary>
public enum RideDocumentType
{
    Invoice = 1,
    CreditNote = 2,
    DebitNote = 3,
    Retention = 4,
    ShippingGuide = 5,
    PurchaseSettlement = 6,
}

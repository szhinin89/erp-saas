namespace ERP.Domain.Modules.Purchases.PurchaseReception.Enums;

/// <summary>
/// Ciclo de vida del documento de recepción persistido. Fase 2 solo produce/consume
/// <see cref="Imported"/> — los demás valores quedan modelados para las fases futuras de
/// conciliación (Verified) y vínculo/creación de compra (Processed).
/// No incluye estados relacionados con el ciclo SRI (ese ciclo pertenece a ElectronicDocuments).
/// </summary>
public enum PurchaseReceptionDocumentStatus
{
    Imported = 1,
    Verified = 2,
    Processed = 3,
    Cancelled = 4,
}

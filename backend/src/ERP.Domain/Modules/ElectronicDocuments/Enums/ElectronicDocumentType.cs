namespace ERP.Domain.Modules.ElectronicDocuments.Enums;

/// <summary>
/// Tipo de comprobante electrónico SRI que administra el módulo transversal ElectronicDocuments.
/// Agregar un nuevo tipo documental no requiere cambios de esquema — solo un nuevo valor aquí.
/// Enum técnico de proceso (vocabulario interno para Strategy pattern: XML builders, validadores
/// de esquema, resolutores de datos) — sus valores ordinales NO son el código SRI <c>codDoc</c>.
/// La fuente fiscal del código SRI ("01","04","07"...) es <c>SriDocumentTypeCodes</c>/<c>SriDocType</c>,
/// transportada por el campo <c>DocTypeCode</c> (string), nunca por este enum.
/// </summary>
public enum ElectronicDocumentType
{
    Invoice = 1,
    CreditNote = 2,
    DebitNote = 3,
    Retention = 4,
    ShippingGuide = 5,
    PurchaseSettlement = 6,
}

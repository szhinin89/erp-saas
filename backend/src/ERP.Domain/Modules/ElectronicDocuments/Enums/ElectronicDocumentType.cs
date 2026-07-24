namespace ERP.Domain.Modules.ElectronicDocuments.Enums;

/// <summary>
/// Tipo de comprobante electrónico SRI que administra el módulo transversal ElectronicDocuments.
/// Agregar un nuevo tipo documental no requiere cambios de esquema — solo un nuevo valor aquí.
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

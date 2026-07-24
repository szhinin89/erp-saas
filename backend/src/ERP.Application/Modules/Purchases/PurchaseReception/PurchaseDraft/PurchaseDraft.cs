using ERP.Application.Modules.Purchases.PurchaseReception.XmlParsing;

namespace ERP.Application.Modules.Purchases.PurchaseReception.PurchaseDraft;

/// <summary>
/// Compra lista para editar en el formulario de Nueva Compra — modelo temporal, nunca persistido.
/// Combina lo extraído del XML (<see cref="ParsedPurchaseXml"/>) con los metadatos ya conocidos y
/// validados por el propio <c>PurchaseReceptionDocument</c> (proveedor ya resuelto si existía al
/// importar, clave de acceso y autorización SRI ya verificadas al descargar el XML).
/// </summary>
public sealed record PurchaseDraft(
    Guid? SupplierId, string SupplierRuc, string SupplierName,
    string DocTypeCode, string InvoiceNumber, DateOnly IssueDate,
    string? AccessKey, string? AuthorizationNumber, DateTime? AuthorizationDate,
    string? SriPaymentMethodCode,
    IReadOnlyList<ParsedPurchaseXmlLine> Lines)
{
    public static PurchaseDraft FromParsedXml(
        ParsedPurchaseXml xml, Guid? supplierId,
        string? accessKey, string? authorizationNumber, DateTime? authorizationDate) => new(
        SupplierId: supplierId,
        SupplierRuc: xml.SupplierRuc,
        SupplierName: xml.SupplierName,
        DocTypeCode: xml.DocTypeCode,
        InvoiceNumber: xml.InvoiceNumber,
        IssueDate: xml.IssueDate,
        AccessKey: accessKey,
        AuthorizationNumber: authorizationNumber,
        AuthorizationDate: authorizationDate,
        SriPaymentMethodCode: xml.SriPaymentMethodCode,
        Lines: xml.Lines);
}

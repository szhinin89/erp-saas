namespace ERP.Domain.Modules.Purchases;

/// <summary>
/// PURCHASE-P2-P3-CLEANUP-CLOSE-01 — códigos técnicos SRI de categoría de impuesto
/// (&lt;impuesto&gt;/&lt;codigo&gt; del comprobante electrónico), estructuralmente fijos por el
/// protocolo SRI — no catálogo editable, por eso viven como constantes y no en BD. Única fuente:
/// antes duplicados como consts privados en <c>PurchaseInvoiceDetail</c>, <c>TaxHelper</c>/
/// <c>ReceptionTaxHelper</c> (PurchaseDraftUseCases.cs), <c>PurchaseXmlDraftParser</c>,
/// <c>PurchaseReceptionXmlViewExtractor</c> y <c>GetPurchaseReceptionXmlViewHandler</c>.
/// </summary>
public static class SriTaxCategoryCodes
{
    public const string Vat = "2";
    public const string Ice = "3";
    public const string Irbpnr = "5";
}

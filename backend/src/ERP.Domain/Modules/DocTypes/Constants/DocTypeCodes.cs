namespace ERP.Domain.Modules.DocTypes.Constants;

/// <summary>
/// Códigos fijos de <see cref="ERP.Domain.Modules.DocTypes.Entities.DocType"/> con un consumidor
/// literal real en código (p. ej. reglas por tipo en <c>DocWorkflowPolicyBootstrapStep</c>). El
/// catálogo administrable completo vive en la tabla <c>doc_type</c>, expuesto vía
/// <c>ERP.Application.Modules.Catalog</c>. Ningún módulo debe asumir que un código listado aquí
/// sigue activo sin consultar el catálogo — mismo criterio que
/// <see cref="ERP.Domain.Modules.SriCatalogs.Constants.SriDocumentTypeCodes"/>.
/// </summary>
public static class DocTypeCodes
{
    public const string SalesInvoice = "FACVEN";
    public const string SalesCreditNote = "NCVDEV";
    public const string PurchaseInvoice = "FACCOM";
    public const string PurchaseCreditNote = "NCCDEV";
    public const string ExpenseDocument = "GASDOC";
    public const string ExpenseWithholding = "RETGAS";
    public const string SupplierPayment = "PAGPRO";
    public const string CustomerCollection = "COBCLI";
    public const string ManualJournalEntry = "ASI";
    public const string InventoryAdjustment = "AJUINV";
}

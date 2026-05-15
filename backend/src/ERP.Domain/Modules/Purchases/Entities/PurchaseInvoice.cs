using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Factura RECIBIDA de proveedor. La empresa NO la emite — la recibe.
/// Por eso NO extiende ElectronicDoc (que es solo para comprobantes emitidos).
/// </summary>
public class PurchaseInvoice
{
    public Guid     Id              { get; set; }
    public Guid     CompanyId       { get; set; }
    public Guid     SupplierId      { get; set; }
    /// <summary>Número de factura del proveedor: "001-001-000000123".</summary>
    public string   InvoiceNumber   { get; set; } = null!;
    /// <summary>Clave de acceso SRI (49 dígitos) si la recibimos electrónicamente.</summary>
    public string?  AccessKey       { get; set; }
    public string?  XmlPath         { get; set; }
    public string   DocType         { get; set; } = "01";
    public DateOnly InvoiceDate     { get; set; }
    public DateOnly? DueDate        { get; set; }
    // Montos
    public decimal  Subtotal        { get; set; }
    public decimal  VatTotal        { get; set; }
    public decimal  Total           { get; set; }
    /// <summary>Total de notas de crédito del proveedor aplicadas a esta factura.</summary>
    public decimal  NotesApplied    { get; set; }
    // net_payable = Total - NotesApplied (calculado en la app, no columna generada)
    public string?  PaymentTerms    { get; set; }
    public string?  TaxSupportCode  { get; set; }
    // Estado: draft | validated | approved | rejected
    public string   Status          { get; set; } = "draft";
    // Flujo de aprobación
    public Guid?    ValidatedBy     { get; set; }
    public DateTime? ValidatedAt    { get; set; }
    public Guid?    ApprovedBy      { get; set; }
    public DateTime? ApprovedAt     { get; set; }
    public Guid?    RejectedBy      { get; set; }
    public DateTime? RejectedAt     { get; set; }
    public string?  RejectionReason { get; set; }
    // Contabilidad
    public Guid?    JournalEntryId  { get; set; }
    public string?  Notes           { get; set; }
    public DateTime CreatedAt       { get; set; }
    public DateTime UpdatedAt       { get; set; }
    public Guid?    CreatedBy       { get; set; }

    // Navigation
    public SriTaxSupport?                    TaxSupport    { get; set; }
    public ICollection<PurchaseInvoiceDetail> Lines         { get; set; } = [];
    public ICollection<SupplierNote>         SupplierNotes { get; set; } = [];
}

namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Nota de crédito o débito recibida de un proveedor.
/// Reduce el saldo pendiente de la factura de compra relacionada.
/// </summary>
public class SupplierNote
{
    public Guid     Id             { get; set; }
    public Guid     CompanyId      { get; set; }
    public Guid     BusinessPartnerId     { get; set; }
    public Guid?    InvoiceId      { get; set; }
    public string   NoteNumber     { get; set; } = null!;
    public string?  AccessKey      { get; set; }
    /// <summary>"04" = nota de crédito | "05" = nota de débito.</summary>
    public string   DocType        { get; set; } = null!;
    public DateOnly NoteDate       { get; set; }
    public string?  Reason         { get; set; }
    public decimal  Subtotal       { get; set; }
    public decimal  VatAmount      { get; set; }
    public decimal  Total          { get; set; }
    public string   Status         { get; set; } = "draft";
    public Guid?    JournalEntryId { get; set; }
    public DateTime CreatedAt      { get; set; }
    public Guid?    CreatedBy      { get; set; }

    public PurchaseInvoice?              Invoice { get; set; }
    public ICollection<SupplierNoteDetail> Lines { get; set; } = [];
}

namespace ERP.Domain.Modules.Purchases.Entities;

public class SupplierNoteDetail
{
    public Guid    Id          { get; set; }
    public Guid    NoteId      { get; set; }
    public Guid?   ProductId   { get; set; }
    public string  Description { get; set; } = null!;
    public decimal Qty         { get; set; }
    public decimal UnitPrice   { get; set; }
    public decimal Subtotal    { get; set; }
    public string? VatCode     { get; set; }
    public decimal VatAmount   { get; set; }
    public decimal Total       { get; set; }

    public SupplierNote Note { get; set; } = null!;
}

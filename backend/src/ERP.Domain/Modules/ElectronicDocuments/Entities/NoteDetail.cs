namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>Líneas de notas de crédito (04) y débito (05).</summary>
public class NoteDetail
{
    public Guid    Id            { get; set; }
    public Guid    DocId         { get; set; }
    public Guid?   ProductId     { get; set; }
    public string? ProductCode   { get; set; }
    public string  Description   { get; set; } = null!;
    public string? UnitCode      { get; set; }
    public decimal Qty           { get; set; }
    public decimal UnitPrice     { get; set; }
    public decimal DiscountPct   { get; set; }
    public string  VatCode       { get; set; } = null!;
    public decimal VatPercentage { get; set; }
    public decimal Subtotal      { get; set; }
    public decimal VatAmount     { get; set; }
    public decimal Total         { get; set; }
    public short   SortOrder     { get; set; }

    public ElectronicDoc Doc { get; set; } = null!;
}

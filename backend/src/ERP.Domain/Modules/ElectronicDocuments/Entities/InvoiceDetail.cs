using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Líneas de factura (01) y liquidación de compra (03).
/// Los campos product_code y description son snapshot inmutable tras autorización.
/// </summary>
public class InvoiceDetail
{
    public Guid    Id               { get; set; }
    public Guid    DocId            { get; set; }
    public Guid?   ProductId        { get; set; }
    public string? ProductCode      { get; set; }
    public string  Description      { get; set; } = null!;
    public string? UnitCode         { get; set; }
    public decimal Qty              { get; set; }
    public decimal UnitPrice        { get; set; }
    public decimal DiscountPct      { get; set; }
    public decimal DiscountAmount   { get; set; }
    public string  VatCode          { get; set; } = null!;
    public decimal VatPercentage    { get; set; }
    public string? IceCode          { get; set; }
    public decimal? IcePercentage   { get; set; }
    public decimal Subtotal         { get; set; }
    public decimal VatAmount        { get; set; }
    public decimal IceAmount        { get; set; }
    public decimal Total            { get; set; }
    /// <summary>JSONB: detallesAdicionales SRI por línea. Parsear como Dictionary&lt;string,string&gt;.</summary>
    public string? AdditionalDetail { get; set; }
    public short   SortOrder        { get; set; }

    public ElectronicDoc Doc     { get; set; } = null!;
    public SriVatRate    VatRate { get; set; } = null!;
}

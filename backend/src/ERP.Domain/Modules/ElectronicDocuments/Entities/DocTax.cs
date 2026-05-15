namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>Desglose totalConImpuestos del comprobante (campo obligatorio en el XML SRI).</summary>
public class DocTax
{
    public Guid    Id          { get; set; }
    public Guid    DocId       { get; set; }
    public short   TaxType     { get; set; }  // 2=IVA  3=ICE  5=IRBPNR  6=ISD
    public string  TaxCode     { get; set; } = null!;
    public decimal TaxableBase { get; set; }
    public decimal Percentage  { get; set; }
    public decimal TaxAmount   { get; set; }

    public ElectronicDoc Doc { get; set; } = null!;
}

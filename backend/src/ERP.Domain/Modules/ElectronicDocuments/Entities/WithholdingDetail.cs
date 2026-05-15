using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Líneas del comprobante de retención emitido (07).
/// SRI requiere referenciar el comprobante retenido en cada línea.
/// </summary>
public class WithholdingDetail
{
    public Guid    Id               { get; set; }
    public Guid    DocId            { get; set; }
    public string  TaxType          { get; set; } = null!;  // IVA | RENTA | ISD
    public string  RetentionCode    { get; set; } = null!;
    public string? RetainedDocType  { get; set; }
    public string? RetainedDocNum   { get; set; }
    public DateOnly? RetainedDocDate { get; set; }
    public decimal TaxableBase      { get; set; }
    public decimal RetentionPct     { get; set; }
    public decimal AmountRetained   { get; set; }
    public string? TaxSupportCode   { get; set; }

    public ElectronicDoc  Doc        { get; set; } = null!;
    public SriTaxSupport? TaxSupport { get; set; }
}

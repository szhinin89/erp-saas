namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>Línea del comprobante de retención recibido.</summary>
public class ReceivedWhDetail
{
    public Guid    Id                { get; set; }
    public Guid    WithholdingId     { get; set; }
    public string  TaxType           { get; set; } = null!;  // IVA | RENTA | ISD
    public string  RetentionCode     { get; set; } = null!;
    public decimal TaxableBase       { get; set; }
    public decimal RetentionPct      { get; set; }
    public decimal AmountRetained    { get; set; }
    /// <summary>Número de la factura de venta a la que aplica esta línea.</summary>
    public string? RelatedInvoiceNum { get; set; }

    public ReceivedWithholding Withholding { get; set; } = null!;
}

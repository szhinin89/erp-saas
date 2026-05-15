using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>Desglose de formas de pago por comprobante (requerido por SRI).</summary>
public class DocPayment
{
    public Guid    Id            { get; set; }
    public Guid    DocId         { get; set; }
    public string  PaymentMethod { get; set; } = null!;
    public decimal Amount        { get; set; }
    public short?  PaymentTerm   { get; set; }
    public string? Bank          { get; set; }
    public string? AccountNumber { get; set; }

    public ElectronicDoc    Doc    { get; set; } = null!;
    public SriPaymentMethod Method { get; set; } = null!;
}

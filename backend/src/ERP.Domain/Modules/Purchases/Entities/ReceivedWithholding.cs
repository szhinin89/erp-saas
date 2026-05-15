namespace ERP.Domain.Modules.Purchases.Entities;

/// <summary>
/// Comprobante de retención RECIBIDO de un cliente.
/// El cliente nos retiene IVA y/o Renta sobre nuestras facturas de venta.
/// No lo emitimos nosotros — lo registramos al recibirlo.
/// </summary>
public class ReceivedWithholding
{
    public Guid     Id            { get; set; }
    public Guid     CompanyId     { get; set; }
    public Guid?    CustomerId    { get; set; }
    /// <summary>Clave de acceso del comprobante emitido por el cliente (49 dígitos).</summary>
    public string?  AccessKey     { get; set; }
    /// <summary>RUC del cliente que emitió la retención.</summary>
    public string   IssuerRuc     { get; set; } = null!;
    public string   IssuerName    { get; set; } = null!;
    public DateOnly IssueDate     { get; set; }
    /// <summary>Factura de venta relacionada (nuestra) que motivó la retención.</summary>
    public Guid?    SalesDocId    { get; set; }
    public decimal  TotalRetained { get; set; }
    public string   Status        { get; set; } = "active";
    public string?  XmlPath       { get; set; }
    public Guid?    JournalEntryId { get; set; }
    public string?  Notes         { get; set; }
    public DateTime CreatedAt     { get; set; }
    public DateTime UpdatedAt     { get; set; }
    public Guid?    CreatedBy     { get; set; }

    public ICollection<ReceivedWhDetail> Details { get; set; } = [];
}

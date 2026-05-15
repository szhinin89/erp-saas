namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Extensión 1:1 de ElectronicDoc para guías de remisión (doc_type 06).
/// Requerida cuando se transportan bienes de un punto a otro.
/// </summary>
public class DeliveryGuide
{
    public Guid    Id             { get; set; }
    public Guid    CompanyId      { get; set; }
    public Guid?   SalesInvoiceId { get; set; }
    // Transporte (campos obligatorios SRI)
    public DateOnly StartDate     { get; set; }
    public DateOnly EndDate       { get; set; }
    public string? CarrierRuc     { get; set; }
    public string? CarrierName    { get; set; }
    public string? CarrierPlate   { get; set; }
    public string? Route          { get; set; }
    // Destino
    public string? DestIdType     { get; set; }
    public string? DestIdNumber   { get; set; }
    public string? DestName       { get; set; }
    public string  DestAddress    { get; set; } = null!;
    public Guid?   CustomerId     { get; set; }

    public ElectronicDoc  ElectronicDoc    { get; set; } = null!;
    public ElectronicDoc? SalesInvoiceDoc  { get; set; }
}

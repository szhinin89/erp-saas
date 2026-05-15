using ERP.Domain.Modules.SriCatalogs.Entities;

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Extensión 1:1 de ElectronicDoc para facturas de venta (doc_type 01).
/// Su Id ES el mismo FK a ElectronicDoc.Id.
/// Los campos buyer_* son snapshot legal INMUTABLE tras autorización.
/// </summary>
public class SalesInvoice
{
    public Guid    Id              { get; set; }
    public Guid    CompanyId       { get; set; }
    // Snapshot comprador (inmutable tras autorización SRI)
    public string  BuyerIdType     { get; set; } = null!;
    public string  BuyerIdNumber   { get; set; } = null!;
    public string  BuyerName       { get; set; } = null!;
    public string? BuyerAddress    { get; set; }
    public string? BuyerEmail      { get; set; }
    public string? BuyerPhone      { get; set; }
    // FK vivo (puede diferir si el cliente actualizó sus datos posteriormente)
    public Guid?   CustomerId      { get; set; }
    public Guid?   WarehouseId     { get; set; }
    public Guid?   SalespersonId   { get; set; }
    public DateOnly? DeliveryDate  { get; set; }
    public string? RemittanceKey   { get; set; }
    public string? GuideDocNum     { get; set; }

    public ElectronicDoc ElectronicDoc   { get; set; } = null!;
    public SriIdType     BuyerIdTypeNav  { get; set; } = null!;
}

namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>Líneas de guía de remisión (06).</summary>
public class DeliveryDetail
{
    public Guid    Id          { get; set; }
    public Guid    DocId       { get; set; }
    public Guid?   ProductId   { get; set; }
    public string  Description { get; set; } = null!;
    public decimal Qty         { get; set; }
    public string? UnitCode    { get; set; }

    public ElectronicDoc Doc { get; set; } = null!;
}

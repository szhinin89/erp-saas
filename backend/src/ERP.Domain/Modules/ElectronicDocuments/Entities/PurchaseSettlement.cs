namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Extensión 1:1 de ElectronicDoc para liquidaciones de compra (doc_type 03).
/// Se emite cuando la empresa compra a personas sin RUC.
/// La empresa actúa como agente de retención.
/// </summary>
public class PurchaseSettlement
{
    public Guid    Id            { get; set; }
    public Guid    CompanyId     { get; set; }
    public string? SellerIdType  { get; set; }
    public string  SellerIdNum   { get; set; } = null!;
    public string  SellerName    { get; set; } = null!;
    public string? SellerAddress { get; set; }
    public Guid?   WarehouseId   { get; set; }

    public ElectronicDoc ElectronicDoc { get; set; } = null!;
}

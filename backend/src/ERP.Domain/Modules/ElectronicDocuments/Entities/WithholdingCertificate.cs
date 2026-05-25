namespace ERP.Domain.Modules.ElectronicDocuments.Entities;

/// <summary>
/// Extensión 1:1 de ElectronicDoc para comprobantes de retención
/// EMITIDOS por la empresa a sus proveedores (doc_type 07).
/// Los campos supplier_* son snapshot del proveedor en el momento de emisión.
/// </summary>
public class WithholdingCertificate
{
    public Guid    Id            { get; set; }
    public Guid    CompanyId     { get; set; }
    public Guid?   SupplierId    { get; set; }
    public string  SupplierRuc   { get; set; } = null!;
    public string  SupplierName  { get; set; } = null!;
    public Guid?   PurchaseInvId { get; set; }
    public decimal TotalRetained { get; set; }

    public ElectronicDoc                  ElectronicDoc { get; set; } = null!;
    public ICollection<WithholdingDetail> Details       { get; set; } = [];
}

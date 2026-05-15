using ERP.Domain.Common;

namespace ERP.Domain.Modules.Purchasing.Entities;

public sealed class PurchBillLine : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen         = 300;
    public const int SupplierProductCodeMaxLen = 50;

    public Guid    PurchBillId            { get; private set; }
    public Guid?   ProductId              { get; private set; }
    public Guid?   PurchaseOrderLineId    { get; private set; }
    public string  Description            { get; private set; } = null!;
    public string? SupplierProductCode    { get; private set; }
    public decimal Quantity               { get; private set; }
    public decimal UnitPrice              { get; private set; }
    public decimal DiscountPct            { get; private set; }
    public decimal Subtotal               { get; private set; }
    public decimal VatPct                 { get; private set; }
    public decimal VatAmount              { get; private set; }
    public decimal Total                  { get; private set; }

    private PurchBillLine() { }

    public void LinkPurchaseOrderLine(Guid purchaseOrderLineId, Guid updatedBy)
    {
        PurchaseOrderLineId = purchaseOrderLineId;
        SetUpdated(updatedBy);
    }

    internal static PurchBillLine Create(
        Guid    tenantId,
        Guid    purchBillId,
        string  description,
        string? supplierProductCode,
        Guid?   productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountPct,
        decimal vatPct,
        Guid    createdBy)
    {
        var subtotal  = quantity * unitPrice * (1 - discountPct / 100m);
        var vatAmount = subtotal * (vatPct / 100m);

        var d = new PurchBillLine
        {
            Id                  = Guid.NewGuid(),
            TenantId            = tenantId,
            PurchBillId         = purchBillId,
            ProductId           = productId,
            Description         = description.Trim(),
            SupplierProductCode = string.IsNullOrWhiteSpace(supplierProductCode)
                                   ? null : supplierProductCode.Trim(),
            Quantity            = quantity,
            UnitPrice           = unitPrice,
            DiscountPct         = discountPct,
            Subtotal            = subtotal,
            VatPct              = vatPct,
            VatAmount           = vatAmount,
            Total               = subtotal + vatAmount,
        };
        d.SetCreated(createdBy);
        return d;
    }
}

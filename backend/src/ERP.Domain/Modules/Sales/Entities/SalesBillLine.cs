using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesBillLine : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen = 300;
    public const int ProductCodeMaxLen = 100;

    public Guid    SalesBillId    { get; private set; }
    public Guid    ProductId      { get; private set; }
    /// <summary>Snapshot del SaleCode del producto al momento de facturar.
    /// Se usa como &lt;codigoPrincipal&gt; en el XML SRI.</summary>
    public string  ProductCode    { get; private set; } = null!;
    public decimal Quantity       { get; private set; }
    public decimal UnitPrice      { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Subtotal       { get; private set; }
    public decimal VatTotal       { get; private set; }
    public decimal Total          { get; private set; }
    public string  Description    { get; private set; } = null!;

    private SalesBillLine() { }

    public static SalesBillLine Create(
        Guid    tenantId,
        Guid    productId,
        string  productCode,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal vatTotal,
        string  description,
        Guid    createdBy)
    {
        var subtotal = quantity * unitPrice - discountAmount;
        var total    = subtotal + vatTotal;

        var line = new SalesBillLine
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            SalesBillId = Guid.Empty,
            ProductId   = productId,
            ProductCode    = string.IsNullOrWhiteSpace(productCode)
                               ? productId.ToString()[..8]
                               : productCode.Trim(),
            Quantity       = quantity,
            UnitPrice      = unitPrice,
            DiscountAmount = discountAmount < 0 ? 0 : discountAmount,
            Subtotal       = subtotal < 0 ? 0 : subtotal,
            VatTotal       = vatTotal,
            Total       = total,
            Description = description.Trim(),
        };
        line.SetCreated(createdBy);
        return line;
    }

    public void AssignBillId(Guid salesBillId)
    {
        if (SalesBillId != Guid.Empty)
            throw new InvalidOperationException("Line is already assigned to a bill.");
        SalesBillId = salesBillId;
    }
}

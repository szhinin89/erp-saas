using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesBillLine : AuditableEntity, ITenantEntity
{
    public const int DescriptionMaxLen = 300;

    public Guid    SalesBillId    { get; private set; }
    public Guid    ProductId      { get; private set; }
    public decimal Quantity       { get; private set; }
    public decimal UnitPrice      { get; private set; }
    public decimal Subtotal       { get; private set; }
    public decimal VatTotal       { get; private set; }
    public decimal Total          { get; private set; }
    public string  Description    { get; private set; } = null!;

    private SalesBillLine() { }

    public static SalesBillLine Create(
        Guid    tenantId,
        Guid    productId,
        decimal quantity,
        decimal unitPrice,
        decimal vatTotal,
        string  description,
        Guid    createdBy)
    {
        var subtotal = quantity * unitPrice;
        var total    = subtotal + vatTotal;

        var line = new SalesBillLine
        {
            Id          = Guid.NewGuid(),
            TenantId    = tenantId,
            SalesBillId = Guid.Empty,
            ProductId   = productId,
            Quantity    = quantity,
            UnitPrice   = unitPrice,
            Subtotal    = subtotal,
            VatTotal    = vatTotal,
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

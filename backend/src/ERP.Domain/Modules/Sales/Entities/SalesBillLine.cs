using ERP.Domain.Common;

namespace ERP.Domain.Modules.Sales.Entities;

public sealed class SalesBillLine : AuditableEntity, ISubscriberScopedEntity
{
    public const int DescriptionMaxLen = 300;
    public const int ProductCodeMaxLen = 100;
    public const int VatCodeMaxLen     = 2;

    public Guid    SalesBillId    { get; private set; }
    public Guid    ProductId      { get; private set; }
    /// <summary>Snapshot del SaleCode del producto al momento de facturar.
    /// Se usa como &lt;codigoPrincipal&gt; en el XML SRI.</summary>
    public string  ProductCode    { get; private set; } = null!;
    public decimal Quantity       { get; private set; }
    public decimal UnitPrice      { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal Subtotal       { get; private set; }
    /// <summary>Código SRI del tipo de IVA: "0"=0%, "2"=12%, "3"=14%, "4"=15%, "5"=5%.</summary>
    public string  VatCode        { get; private set; } = "0";
    /// <summary>Porcentaje de IVA (ej: 15.00 para 15%). Snapshot inmutable.</summary>
    public decimal VatPercentage  { get; private set; }
    public decimal VatTotal       { get; private set; }
    public decimal Total          { get; private set; }
    public string  Description    { get; private set; } = null!;

    private SalesBillLine() { }

    public static SalesBillLine Create(
        Guid    subscriberId,
        Guid    productId,
        string  productCode,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        string  vatCode,
        decimal vatPercentage,
        decimal vatTotal,
        string  description,
        Guid    createdBy)
    {
        var subtotal = quantity * unitPrice - discountAmount;
        var total    = subtotal + vatTotal;

        var line = new SalesBillLine
        {
            Id          = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            SalesBillId = Guid.Empty,
            ProductId   = productId,
            ProductCode    = string.IsNullOrWhiteSpace(productCode)
                               ? productId.ToString()[..8]
                               : productCode.Trim(),
            Quantity       = quantity,
            UnitPrice      = unitPrice,
            DiscountAmount = discountAmount < 0 ? 0 : discountAmount,
            Subtotal       = subtotal < 0 ? 0 : subtotal,
            VatCode        = string.IsNullOrWhiteSpace(vatCode) ? "0" : vatCode.Trim(),
            VatPercentage  = vatPercentage < 0 ? 0 : vatPercentage,
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

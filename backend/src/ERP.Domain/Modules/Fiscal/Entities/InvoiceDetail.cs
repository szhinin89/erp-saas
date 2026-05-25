namespace ERP.Domain.Modules.Fiscal.Entities;

public sealed class InvoiceDetail
{
    public const int NameMaxLen = 200;
    public const int SkuMaxLen = 60;
    public const int UnitMaxLen = 40;
    public const int VatCodeMaxLen = 2;
    public const int DescriptionMaxLen = 300;

    public long Id { get; private set; }
    public long InvoiceId { get; private set; }
    public Guid SubscriberId { get; private set; }
    public Guid BranchId { get; private set; }
    public short LineNo { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = null!;
    public string SkuSnapshot { get; private set; } = null!;
    public string UnitNameSnapshot { get; private set; } = null!;
    public string DescriptionSnapshot { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPriceSnapshot { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public string TaxCodeSnapshot { get; private set; } = "0";
    public decimal TaxRateSnapshot { get; private set; }
    public decimal LineSubtotal { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }
    public DateOnly IssueDate { get; private set; }

    private InvoiceDetail() { }

    public static InvoiceDetail Create(
        long id,
        long invoiceId,
        Guid subscriberId,
        Guid branchId,
        short lineNo,
        Guid productId,
        string productName,
        string sku,
        string unitName,
        string description,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        string taxCode,
        decimal taxRate,
        decimal lineTax,
        DateOnly issueDate)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));

        var discount = discountAmount < 0 ? 0 : discountAmount;
        var subtotal = Math.Max(0, quantity * unitPrice - discount);
        var tax = lineTax;
        var total = subtotal + tax;

        return new InvoiceDetail
        {
            Id = id,
            InvoiceId = invoiceId,
            SubscriberId = subscriberId,
            BranchId = branchId,
            LineNo = lineNo,
            ProductId = productId,
            ProductNameSnapshot = productName.Trim(),
            SkuSnapshot = sku.Trim(),
            UnitNameSnapshot = unitName.Trim(),
            DescriptionSnapshot = description.Trim(),
            Quantity = quantity,
            UnitPriceSnapshot = unitPrice,
            DiscountAmount = discount,
            TaxCodeSnapshot = string.IsNullOrWhiteSpace(taxCode) ? "0" : taxCode.Trim(),
            TaxRateSnapshot = taxRate,
            LineSubtotal = subtotal,
            LineTax = tax,
            LineTotal = total,
            IssueDate = issueDate,
        };
    }

    internal void AssignId(long id) => Id = id;
}

namespace ERP.Domain.Modules.Commercial.Entities;

public sealed class QuoteDetail
{
    public const int NameMaxLen = 200;
    public const int SkuMaxLen = 60;
    public const int UnitMaxLen = 40;

    public long Id { get; private set; }
    public long QuoteId { get; private set; }
    public Guid SubscriberId { get; private set; }
    public Guid BranchId { get; private set; }
    public short LineNo { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductNameSnapshot { get; private set; } = null!;
    public string SkuSnapshot { get; private set; } = null!;
    public string UnitNameSnapshot { get; private set; } = null!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxRateSnapshot { get; private set; }
    public decimal LineSubtotal { get; private set; }
    public decimal LineTax { get; private set; }
    public decimal LineTotal { get; private set; }
    public DateOnly IssueDate { get; private set; }

    private QuoteDetail() { }

    public static QuoteDetail Create(
        long id,
        long quoteId,
        Guid subscriberId,
        Guid branchId,
        short lineNo,
        Guid productId,
        string productName,
        string sku,
        string unitName,
        decimal quantity,
        decimal unitPrice,
        decimal taxRate,
        DateOnly issueDate)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));

        var subtotal = Math.Round(quantity * unitPrice, 4, MidpointRounding.AwayFromZero);
        var tax = Math.Round(subtotal * taxRate / 100m, 4, MidpointRounding.AwayFromZero);
        var total = subtotal + tax;

        return new QuoteDetail
        {
            Id = id,
            QuoteId = quoteId,
            SubscriberId = subscriberId,
            BranchId = branchId,
            LineNo = lineNo,
            ProductId = productId,
            ProductNameSnapshot = productName.Trim(),
            SkuSnapshot = sku.Trim(),
            UnitNameSnapshot = unitName.Trim(),
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxRateSnapshot = taxRate,
            LineSubtotal = subtotal,
            LineTax = tax,
            LineTotal = total,
            IssueDate = issueDate,
        };
    }

    internal void AssignId(long id) => Id = id;
}

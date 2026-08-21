namespace ZH.PrintAgent.Contracts;

public sealed record ReceiptDocument
{
    public string MerchantName { get; init; } = string.Empty;
    public IReadOnlyList<string> HeaderLines { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ReceiptLineItem> Items { get; init; } = Array.Empty<ReceiptLineItem>();
    public IReadOnlyList<ReceiptTotalLine> Totals { get; init; } = Array.Empty<ReceiptTotalLine>();
    public IReadOnlyList<string> FooterLines { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RawLines { get; init; } = Array.Empty<string>();
}

public sealed record ReceiptLineItem
{
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Total { get; init; }
}

public sealed record ReceiptTotalLine
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

namespace ERP.Domain.Modules.Expenses.Enums;

public enum ExpenseDocumentType
{
    Invoice,
    Receipt,
    Other
}

public static class ExpenseDocumentTypeExtensions
{
    public static string ToDbValue(this ExpenseDocumentType type) => type switch
    {
        ExpenseDocumentType.Invoice => "INVOICE",
        ExpenseDocumentType.Receipt => "RECEIPT",
        ExpenseDocumentType.Other   => "OTHER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static ExpenseDocumentType FromDbValue(string value) => value switch
    {
        "RECEIPT" => ExpenseDocumentType.Receipt,
        "OTHER"   => ExpenseDocumentType.Other,
        _         => ExpenseDocumentType.Invoice
    };
}

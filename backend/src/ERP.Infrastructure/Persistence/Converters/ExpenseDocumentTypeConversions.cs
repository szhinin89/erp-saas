using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones entre ExpenseDocumentType (dominio) y valores de BD.
/// Consumer: EF value converter en ExpenseDocumentConfiguration.
/// </summary>
internal static class ExpenseDocumentTypeConversions
{
    internal static string ToDb(ExpenseDocumentType type) => type switch
    {
        ExpenseDocumentType.Invoice => "INVOICE",
        ExpenseDocumentType.Receipt => "RECEIPT",
        ExpenseDocumentType.Other   => "OTHER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    internal static ExpenseDocumentType FromDb(string value) => value switch
    {
        "RECEIPT" => ExpenseDocumentType.Receipt,
        "OTHER"   => ExpenseDocumentType.Other,
        _         => ExpenseDocumentType.Invoice
    };
}

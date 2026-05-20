namespace ERP.Domain.Modules.Sales.Enums;

public enum SalesDocumentType
{
    Invoice,
    CreditNote,
    DebitNote,
    Proforma
}

public static class SalesDocumentTypeExtensions
{
    public static string ToDbValue(this SalesDocumentType type) => type switch
    {
        SalesDocumentType.Invoice    => "INVOICE",
        SalesDocumentType.CreditNote => "CREDIT_NOTE",
        SalesDocumentType.DebitNote  => "DEBIT_NOTE",
        SalesDocumentType.Proforma   => "PROFORMA",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static SalesDocumentType FromNoteType(string noteType) =>
        noteType.Trim().ToUpperInvariant() is "CREDIT" or "CREDITO"
            ? SalesDocumentType.CreditNote
            : SalesDocumentType.DebitNote;

    public static SalesDocumentType FromLegacyCode(string? code) => code?.Trim() switch
    {
        "04" or "CREDIT" or "CREDITO" or "CREDIT_NOTE" => SalesDocumentType.CreditNote,
        "05" or "DEBIT" or "DEBITO" or "DEBIT_NOTE"  => SalesDocumentType.DebitNote,
        "PROFORMA" or "proforma"                       => SalesDocumentType.Proforma,
        _                                              => SalesDocumentType.Invoice
    };

    public static string ToLegacySriCode(this SalesDocumentType type) => type switch
    {
        SalesDocumentType.CreditNote => "04",
        SalesDocumentType.DebitNote  => "05",
        SalesDocumentType.Proforma   => "PROFORMA",
        _                            => "01"
    };
}

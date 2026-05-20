namespace ERP.Domain.Modules.Purchasing.Enums;

public enum PurchaseDocumentType
{
    Invoice,
    CreditNote,
    DebitNote,
    Order
}

public static class PurchaseDocumentTypeExtensions
{
    public static string ToDbValue(this PurchaseDocumentType type) => type switch
    {
        PurchaseDocumentType.Invoice    => "INVOICE",
        PurchaseDocumentType.CreditNote => "CREDIT_NOTE",
        PurchaseDocumentType.DebitNote  => "DEBIT_NOTE",
        PurchaseDocumentType.Order      => "ORDER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    public static PurchaseDocumentType FromDbValue(string value) => value switch
    {
        "CREDIT_NOTE" => PurchaseDocumentType.CreditNote,
        "DEBIT_NOTE"  => PurchaseDocumentType.DebitNote,
        "ORDER"       => PurchaseDocumentType.Order,
        _             => PurchaseDocumentType.Invoice
    };

    public static PurchaseDocumentType FromSupplierNoteType(string noteType) =>
        noteType.Trim().ToUpperInvariant() is "CREDIT" or "CREDITO"
            ? PurchaseDocumentType.CreditNote
            : PurchaseDocumentType.DebitNote;
}

using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones entre PurchaseDocumentType (dominio) y representaciones externas.
/// Consumers: EF value converter (PurchaseDocumentConfiguration), PurchaseDocumentMapper.
/// </summary>
internal static class PurchaseDocumentTypeConversions
{
    internal static string ToDb(PurchaseDocumentType type) => type switch
    {
        PurchaseDocumentType.Invoice    => "INVOICE",
        PurchaseDocumentType.CreditNote => "CREDIT_NOTE",
        PurchaseDocumentType.DebitNote  => "DEBIT_NOTE",
        PurchaseDocumentType.Order      => "ORDER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    internal static PurchaseDocumentType FromDb(string value) => value switch
    {
        "CREDIT_NOTE" => PurchaseDocumentType.CreditNote,
        "DEBIT_NOTE"  => PurchaseDocumentType.DebitNote,
        "ORDER"       => PurchaseDocumentType.Order,
        _             => PurchaseDocumentType.Invoice
    };

    /// <summary>
    /// Maps SupplierNote.NoteType ("CREDIT"/"DEBIT") to enum.
    /// Consumer: PurchaseDocumentMapper when rehydrating notes.
    /// </summary>
    internal static PurchaseDocumentType FromNoteType(string noteType) =>
        noteType.Trim().ToUpperInvariant() is "CREDIT" or "CREDITO"
            ? PurchaseDocumentType.CreditNote
            : PurchaseDocumentType.DebitNote;
}

using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones de PurchaseDocumentType (enum) a representaciones externas.
///   ToDb / FromDb → DB persistence (EF value converter)
/// NoteType conversions live in NoteTypeConversions.cs.
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
}

using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones de SalesDocumentType (enum) a representaciones externas.
///   ToDb / FromCode → DB persistence (EF value converter)
///   ToSriDocCode    → SRI XML (codDocSustento)
/// NoteType conversions live in NoteTypeConversions.cs.
/// </summary>
internal static class SalesDocumentTypeConversions
{
    // ── DB persistence ────────────────────────────────────────────────────────

    internal static string ToDb(SalesDocumentType type) => type switch
    {
        SalesDocumentType.Invoice    => "INVOICE",
        SalesDocumentType.CreditNote => "CREDIT_NOTE",
        SalesDocumentType.DebitNote  => "DEBIT_NOTE",
        SalesDocumentType.Proforma   => "PROFORMA",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    internal static SalesDocumentType FromCode(string? code) => code?.Trim() switch
    {
        "04" or "CREDIT_NOTE" => SalesDocumentType.CreditNote,
        "05" or "DEBIT_NOTE"  => SalesDocumentType.DebitNote,
        "PROFORMA"            => SalesDocumentType.Proforma,
        _                     => SalesDocumentType.Invoice
    };

    // ── SRI XML ───────────────────────────────────────────────────────────────

    internal static string ToSriDocCode(SalesDocumentType type) => type switch
    {
        SalesDocumentType.CreditNote => "04",
        SalesDocumentType.DebitNote  => "05",
        SalesDocumentType.Proforma   => "PROFORMA",
        _                            => "01"
    };
}

using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// Conversiones entre SalesDocumentType (dominio) y representaciones externas.
/// Vive en Infrastructure — el dominio no mapea formatos externos.
/// </summary>
internal static class SalesDocumentTypeConversions
{
    /// <summary>DB storage value. Used by EF value converter in SalesDocumentConfiguration.</summary>
    internal static string ToDb(SalesDocumentType type) => type switch
    {
        SalesDocumentType.Invoice    => "INVOICE",
        SalesDocumentType.CreditNote => "CREDIT_NOTE",
        SalesDocumentType.DebitNote  => "DEBIT_NOTE",
        SalesDocumentType.Proforma   => "PROFORMA",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>
    /// Reads from DB value or SRI XML doc code.
    /// Used by EF value converter in SalesDocumentConfiguration.
    /// </summary>
    internal static SalesDocumentType FromCode(string? code) => code?.Trim() switch
    {
        "04" or "CREDIT" or "CREDITO" or "CREDIT_NOTE" => SalesDocumentType.CreditNote,
        "05" or "DEBIT"  or "DEBITO"  or "DEBIT_NOTE"  => SalesDocumentType.DebitNote,
        "PROFORMA" or "proforma"                        => SalesDocumentType.Proforma,
        _                                               => SalesDocumentType.Invoice
    };

    /// <summary>
    /// Maps SalesNote.NoteType field ("CREDIT"/"DEBIT") to enum.
    /// Used by SalesDocumentMapper when rehydrating notes from DB.
    /// </summary>
    internal static SalesDocumentType FromNoteType(string noteType) =>
        noteType.Trim().ToUpperInvariant() is "CREDIT" or "CREDITO"
            ? SalesDocumentType.CreditNote
            : SalesDocumentType.DebitNote;

    /// <summary>
    /// SRI XML doc code (codDocSustento) for electronic invoice generation.
    /// Consumer: SriXmlFacturaBuilder, SalesDocumentMapper.
    /// </summary>
    internal static string ToSriDocCode(SalesDocumentType type) => type switch
    {
        SalesDocumentType.CreditNote => "04",
        SalesDocumentType.DebitNote  => "05",
        SalesDocumentType.Proforma   => "PROFORMA",
        _                            => "01"
    };
}

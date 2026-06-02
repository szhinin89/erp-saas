using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// ÚNICA fuente de verdad para conversiones de SalesDocumentType.
/// Cada destino tiene UN solo método:
///   DB persistence  → ToDb / FromCode
///   SalesNote field → ToNoteType / FromNoteType / IsNoteTypeCredit
///   SRI XML         → ToSriDocCode
/// </summary>
internal static class SalesDocumentTypeConversions
{
    // ── DB persistence (EF value converter) ──────────────────────────────────

    /// <summary>Consumer: EF value converter en SalesDocumentConfiguration.</summary>
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
    /// Consumer: EF value converter en SalesDocumentConfiguration.
    /// </summary>
    internal static SalesDocumentType FromCode(string? code) => code?.Trim() switch
    {
        "04" or "CREDIT" or "CREDITO" or "CREDIT_NOTE" => SalesDocumentType.CreditNote,
        "05" or "DEBIT"  or "DEBITO"  or "DEBIT_NOTE"  => SalesDocumentType.DebitNote,
        "PROFORMA" or "proforma"                        => SalesDocumentType.Proforma,
        _                                               => SalesDocumentType.Invoice
    };

    // ── SalesNote.NoteType field ──────────────────────────────────────────────

    /// <summary>
    /// Canonical NoteType string written to SalesNote.NoteType.
    /// Consumer: SalesDocumentMapper.ToLegacyNote().
    /// </summary>
    internal static string ToNoteType(SalesDocumentType type) =>
        type == SalesDocumentType.CreditNote ? "CREDIT" : "DEBIT";

    /// <summary>
    /// Reconstructs enum from SalesNote.NoteType field.
    /// Handles both legacy Spanish ("CREDITO") and canonical English ("CREDIT").
    /// Consumer: SalesDocumentMapper.ToDocument(SalesNote).
    /// </summary>
    internal static SalesDocumentType FromNoteType(string noteType) =>
        IsNoteTypeCredit(noteType)
            ? SalesDocumentType.CreditNote
            : SalesDocumentType.DebitNote;

    /// <summary>
    /// Single check for "is credit note?" from SalesNote.NoteType field.
    /// Handles both "CREDIT" (canonical) and "CREDITO" (legacy DB values).
    /// Consumers: SriXmlFacturaBuilder, RideGeneratorService, SriFacturaElectronicaSimuladoService.
    /// </summary>
    internal static bool IsNoteTypeCredit(string noteType) =>
        noteType.Trim().Equals("CREDIT",  StringComparison.OrdinalIgnoreCase) ||
        noteType.Trim().Equals("CREDITO", StringComparison.OrdinalIgnoreCase);

    // ── SRI XML generation ────────────────────────────────────────────────────

    /// <summary>
    /// SRI XML doc code (codDocSustento) for electronic invoice XML.
    /// Consumers: SriXmlFacturaBuilder, SalesDocumentMapper.
    /// </summary>
    internal static string ToSriDocCode(SalesDocumentType type) => type switch
    {
        SalesDocumentType.CreditNote => "04",
        SalesDocumentType.DebitNote  => "05",
        SalesDocumentType.Proforma   => "PROFORMA",
        _                            => "01"
    };
}

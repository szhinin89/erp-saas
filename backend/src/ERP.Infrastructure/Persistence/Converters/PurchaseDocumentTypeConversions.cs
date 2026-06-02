using ERP.Domain.Modules.Purchasing.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// ÚNICA fuente de verdad para conversiones de PurchaseDocumentType.
/// Cada destino tiene UN solo método:
///   DB persistence  → ToDb / FromDb
///   PurchNote field → ToNoteType / FromNoteType
///   XML parsing     → NoteTypeCredit / NoteTypeDebit (constantes canónicas)
/// </summary>
internal static class PurchaseDocumentTypeConversions
{
    // ── DB persistence (EF value converter) ──────────────────────────────────

    /// <summary>Consumer: EF value converter en PurchaseDocumentConfiguration.</summary>
    internal static string ToDb(PurchaseDocumentType type) => type switch
    {
        PurchaseDocumentType.Invoice    => "INVOICE",
        PurchaseDocumentType.CreditNote => "CREDIT_NOTE",
        PurchaseDocumentType.DebitNote  => "DEBIT_NOTE",
        PurchaseDocumentType.Order      => "ORDER",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
    };

    /// <summary>Consumer: EF value converter en PurchaseDocumentConfiguration.</summary>
    internal static PurchaseDocumentType FromDb(string value) => value switch
    {
        "CREDIT_NOTE" => PurchaseDocumentType.CreditNote,
        "DEBIT_NOTE"  => PurchaseDocumentType.DebitNote,
        "ORDER"       => PurchaseDocumentType.Order,
        _             => PurchaseDocumentType.Invoice
    };

    // ── PurchNote.NoteType field ──────────────────────────────────────────────

    /// <summary>
    /// Canonical NoteType strings for PurchNote.NoteType field and XML parsing.
    /// SriFacturaParser uses these constants when mapping XML root names.
    /// </summary>
    internal const string NoteTypeCredit = "CREDIT";
    internal const string NoteTypeDebit  = "DEBIT";

    /// <summary>
    /// Canonical NoteType string written to PurchNote.NoteType.
    /// Consumer: PurchaseDocumentMapper.ToLegacyNote().
    /// </summary>
    internal static string ToNoteType(PurchaseDocumentType type) =>
        type == PurchaseDocumentType.CreditNote ? NoteTypeCredit : NoteTypeDebit;

    /// <summary>
    /// Reconstructs enum from PurchNote.NoteType field ("CREDIT"/"DEBIT").
    /// Consumer: PurchaseDocumentMapper when rehydrating notes, downstream handlers.
    /// </summary>
    internal static PurchaseDocumentType FromNoteType(string noteType) =>
        noteType.Trim().Equals(NoteTypeCredit, StringComparison.OrdinalIgnoreCase)
            ? PurchaseDocumentType.CreditNote
            : PurchaseDocumentType.DebitNote;
}

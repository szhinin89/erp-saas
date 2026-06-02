using ERP.Domain.Common;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Sales.Enums;

namespace ERP.Infrastructure.Persistence.Converters;

/// <summary>
/// ÚNICA fuente de verdad para conversiones externas de NoteType.
///   ToDb / FromDb       → DB persistence (EF value converter)
///   ToSriDocCode        → SRI XML codDocSustento ("04"/"05")
///   FromXmlRoot         → parse SRI XML root element name
///   ToXmlElementNames   → SRI XML element names for builder
///   ToSalesDocumentType → cross-mapping to SalesDocumentType enum (for unified doc model)
///   ToNoteType          → cross-mapping from SalesDocumentType enum
/// </summary>
internal static class NoteTypeConversions
{
    // ── DB persistence ────────────────────────────────────────────────────────

    internal static string ToDb(NoteType type) =>
        type == NoteType.Credit ? "CREDIT" : "DEBIT";

    /// <summary>
    /// Handles canonical ("CREDIT"/"DEBIT") and legacy Spanish ("CREDITO"/"DEBITO").
    /// Consumer: EF value converters en SalesNoteConfiguration, PurchNoteConfiguration.
    /// </summary>
    internal static NoteType FromDb(string value) =>
        value.Trim().Equals("CREDIT",  StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("CREDITO", StringComparison.OrdinalIgnoreCase)
            ? NoteType.Credit
            : NoteType.Debit;

    // ── SRI XML ───────────────────────────────────────────────────────────────

    /// <summary>
    /// SRI XML codDocSustento. Consumers: SriXmlFacturaBuilder, RideGeneratorService,
    /// SriFacturaElectronicaSimuladoService, SalesDocumentMapper.
    /// </summary>
    internal static string ToSriDocCode(NoteType type) =>
        type == NoteType.Credit ? "04" : "05";

    /// <summary>Consumer: SriFacturaParser — maps XML root element to NoteType.</summary>
    internal static NoteType FromXmlRoot(string rootName) =>
        rootName.Trim().Equals("notaCredito", StringComparison.OrdinalIgnoreCase)
            ? NoteType.Credit
            : NoteType.Debit;

    /// <summary>
    /// SRI XML root and info element names.
    /// Consumers: SriXmlFacturaBuilder, SriFacturaElectronicaSimuladoService.
    /// </summary>
    internal static (string Root, string Info) ToXmlElementNames(NoteType type) =>
        type == NoteType.Credit
            ? ("notaCredito", "infoNotaCredito")
            : ("notaDebito",  "infoNotaDebito");

    // ── Cross-mapping with SalesDocumentType (unified document model) ─────────

    /// <summary>Consumer: SalesDocumentMapper — converting SalesNote to SalesDocument.</summary>
    internal static SalesDocumentType ToSalesDocumentType(NoteType type) =>
        type == NoteType.Credit ? SalesDocumentType.CreditNote : SalesDocumentType.DebitNote;

    /// <summary>Consumer: SalesDocumentMapper — converting SalesDocument back to SalesNote.</summary>
    internal static NoteType FromSalesDocumentType(SalesDocumentType type) =>
        type == SalesDocumentType.CreditNote ? NoteType.Credit : NoteType.Debit;

    /// <summary>Consumer: PurchaseDocumentMapper — converting PurchaseDocument to PurchNote.</summary>
    internal static NoteType FromPurchaseDocumentType(PurchaseDocumentType type) =>
        type == PurchaseDocumentType.CreditNote ? NoteType.Credit : NoteType.Debit;

    /// <summary>Consumer: PurchaseDocumentMapper — converting PurchNote to PurchaseDocument.</summary>
    internal static PurchaseDocumentType ToPurchaseDocumentType(NoteType type) =>
        type == NoteType.Credit ? PurchaseDocumentType.CreditNote : PurchaseDocumentType.DebitNote;
}

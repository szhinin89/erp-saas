namespace ERP.Domain.Modules.Accounting.ValueObjects;

/// <summary>
/// ACCOUNTING-CONTINUITY-06 — asientos de un período que impiden su cierre, por motivo (ver
/// <see cref="JournalEntryClosureReadiness"/> para la semántica de cada uno).
/// </summary>
public sealed record JournalEntryClosureCounts(
    int DraftOrNonFinalEntries,
    int EntriesWithoutEntryNumber,
    int IncompleteReversals
);

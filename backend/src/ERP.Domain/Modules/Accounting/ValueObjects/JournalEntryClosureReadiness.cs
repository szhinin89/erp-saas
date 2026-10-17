namespace ERP.Domain.Modules.Accounting.ValueObjects;

/// <summary>
/// Fase 5.5 (ADR-026 §6.1/§9): resumen de precondiciones cross-aggregate para el cierre de un
/// <c>AccountingPeriod</c>, resuelto en Application (ACCOUNTING-CONTINUITY-06: readiness único de
/// completitud contable) y pasado a <c>AccountingPeriod.Close</c> para que el aggregate rechace el
/// cierre sin conocer <c>JournalEntry</c>, obligaciones ni orígenes directamente — mismo criterio
/// ya usado para el invariante "sin períodos solapados" (ver &lt;remarks&gt; de <c>AccountingPeriod</c>).
/// Todas las banderas son obligatorias: una precondición nueva nunca queda satisfecha por omisión.
/// </summary>
/// <param name="HasDraftOrNonFinalEntries">
/// Existe al menos un <c>JournalEntry</c> del período cuyo <c>Status</c> no es
/// <c>Posted</c> ni <c>Reversed</c> (con el enum actual: Draft).
/// </param>
/// <param name="HasEntriesWithoutEntryNumber">
/// Existe un <c>JournalEntry</c> no-Draft cuyo <c>EntryNumber</c> es nulo — red de seguridad antes
/// de un cierre irreversible (<c>Post</c> siempre asigna el número).
/// </param>
/// <param name="HasIncompleteReversals">
/// Existe un reverso (<c>OriginalJournalEntryId</c> no nulo) que no quedó <c>Posted</c>, o un
/// asiento histórico marcado <c>Reversed</c> sin <c>ReverseJournalEntryId</c> (solo datos previos a
/// ACCOUNTING-CONTINUITY-03: desde entonces el original nunca se muta).
/// </param>
/// <param name="HasUnresolvedPostings">
/// Existe una obligación contable durable (<c>AccountingPosting</c>) Pending o Failed con
/// <c>EntryDate</c> dentro del período (docs/architecture/accounting.md §8).
/// </param>
/// <param name="HasUnresolvedOpening">
/// La apertura sujeta de la Carga Inicial, con fecha dentro del período y aún no cerrada, tiene
/// lotes sin asiento Posted o su ASI vigente ausente/no publicado.
/// </param>
/// <param name="HasPendingSaleCost">
/// Una venta sujeta a Accounting del período conserva costo de ventas pendiente de resolver.
/// </param>
/// <param name="HasOriginsWithoutAccounting">
/// Un documento operativo sujeto del período no tiene obligación contable ni asiento.
/// </param>
/// <param name="HasPreviousOpenPeriod">
/// Existe un período anterior de la misma Company todavía Open (cierre secuencial).
/// </param>
/// <param name="HasYearEndClosingMissing">
/// ACCOUNTING-12D: el período es el último del ejercicio y las cuentas de resultados del ejercicio
/// conservan saldo — falta contabilizar (Posted) el asiento de cierre anual o quedó desactualizado.
/// </param>
public sealed record JournalEntryClosureReadiness(
    bool HasDraftOrNonFinalEntries,
    bool HasEntriesWithoutEntryNumber,
    bool HasIncompleteReversals,
    bool HasUnresolvedPostings,
    bool HasUnresolvedOpening,
    bool HasPendingSaleCost,
    bool HasOriginsWithoutAccounting,
    bool HasPreviousOpenPeriod,
    bool HasYearEndClosingMissing
)
{
    /// <summary>Sin ningún bloqueo — explícito, para los llamadores que cierran un período listo.</summary>
    public static JournalEntryClosureReadiness Ready { get; } =
        new(false, false, false, false, false, false, false, false, false);

    public bool IsReady =>
        !HasDraftOrNonFinalEntries
        && !HasEntriesWithoutEntryNumber
        && !HasIncompleteReversals
        && !HasUnresolvedPostings
        && !HasUnresolvedOpening
        && !HasPendingSaleCost
        && !HasOriginsWithoutAccounting
        && !HasPreviousOpenPeriod
        && !HasYearEndClosingMissing;

    /// <summary>Motivos de bloqueo en español, listos para componer el mensaje de la excepción de dominio.</summary>
    public IEnumerable<string> BuildBlockingReasons()
    {
        if (HasDraftOrNonFinalEntries)
            yield return "existen asientos contables sin publicar (Draft)";
        if (HasEntriesWithoutEntryNumber)
            yield return "existen asientos publicados sin número de asiento asignado";
        if (HasIncompleteReversals)
            yield return "existen reversos contables incompletos";
        if (HasUnresolvedPostings)
            yield return "existen contabilizaciones pendientes o fallidas con fecha dentro del período";
        if (HasUnresolvedOpening)
            yield return "la apertura contable de la carga inicial tiene lotes o ASI sin publicar";
        if (HasPendingSaleCost)
            yield return "existen ventas sujetas con costo de ventas pendiente de resolver";
        if (HasOriginsWithoutAccounting)
            yield return "existen documentos sujetos a Contabilidad sin contabilización registrada";
        if (HasPreviousOpenPeriod)
            yield return "existe un período anterior todavía abierto";
        if (HasYearEndClosingMissing)
            yield return "falta contabilizar el asiento de cierre anual del ejercicio (las cuentas de resultados conservan saldo)";
    }
}

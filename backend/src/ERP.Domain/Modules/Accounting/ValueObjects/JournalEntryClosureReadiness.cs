namespace ERP.Domain.Modules.Accounting.ValueObjects;

/// <summary>
/// Fase 5.5 (ADR-026 §6.1/§9): resumen de precondiciones cross-aggregate para el cierre de un
/// <c>AccountingPeriod</c>, resuelto por <c>IJournalEntryRepository.GetClosureReadinessAsync</c>
/// (Application/Infrastructure) y pasado a <c>AccountingPeriod.Close</c> para que el aggregate
/// rechace el cierre sin conocer <c>JournalEntry</c> directamente — mismo criterio ya usado para
/// el invariante "sin períodos solapados" (ver &lt;remarks&gt; de <c>AccountingPeriod</c>).
/// </summary>
/// <param name="HasDraftOrNonFinalEntries">
/// Existe al menos un <c>JournalEntry</c> del período cuyo <c>Status</c> no es
/// <c>Posted</c> ni <c>Reversed</c>. Cubre a la vez "no existan JournalEntry Draft" y "todos
/// estén Posted o Reversed" — con el enum actual (Draft/Posted/Reversed) ambas reglas de negocio
/// son el mismo predicado; se mantienen como dos requisitos de negocio distintos en la
/// documentación aunque compartan una sola consulta, para no duplicar un EXISTS idéntico.
/// </param>
/// <param name="HasEntriesWithoutEntryNumber">
/// Existe un <c>JournalEntry</c> no-Draft (por lo tanto ya debería haber pasado por <c>Post</c>)
/// cuyo <c>EntryNumber</c> es nulo — invariante que hoy nunca debería violarse (<c>Post</c>
/// siempre asigna el número), pero se valida explícitamente como red de seguridad antes de un
/// cierre irreversible.
/// </param>
/// <param name="HasIncompleteReversals">
/// Existe un <c>JournalEntry</c> marcado <c>Reversed</c> sin <c>ReverseJournalEntryId</c>
/// asignado, o un asiento que es en sí mismo un reverso (<c>OriginalJournalEntryId</c> no nulo)
/// pero no quedó <c>Posted</c> — ambos casos representan un reverso a medio completar.
/// </param>
public sealed record JournalEntryClosureReadiness(
    bool HasDraftOrNonFinalEntries,
    bool HasEntriesWithoutEntryNumber,
    bool HasIncompleteReversals
)
{
    public bool IsReady =>
        !HasDraftOrNonFinalEntries && !HasEntriesWithoutEntryNumber && !HasIncompleteReversals;

    /// <summary>Motivos de bloqueo en español, listos para componer el mensaje de la excepción de dominio.</summary>
    public IEnumerable<string> BuildBlockingReasons()
    {
        if (HasDraftOrNonFinalEntries)
            yield return "existen asientos contables sin publicar (Draft)";
        if (HasEntriesWithoutEntryNumber)
            yield return "existen asientos publicados sin número de asiento asignado";
        if (HasIncompleteReversals)
            yield return "existen reversos contables incompletos";
    }
}

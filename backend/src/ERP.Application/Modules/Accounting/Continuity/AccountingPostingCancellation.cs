using ERP.Application.Common;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — comportamiento común de anulación de un origen contable (Compra,
/// Gasto, Retención), dentro de la transacción de la anulación:
/// <list type="bullet">
/// <item>asiento Posted del hecho → se devuelve para reversarlo con el reverso existente;</item>
/// <item>obligación Pending/Failed sin asiento → Canceled (no se fabrica un reverso);</item>
/// <item>sin asiento ni obligación (Accounting no aplicaba) → nada.</item>
/// </list>
/// Bloquea la obligación antes de leer el libro para serializar con un intento concurrente.
/// </summary>
public sealed class AccountingPostingCancellation
{
    private readonly IAccountingPostingRepository _postings;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly ICurrentUser _user;

    public AccountingPostingCancellation(
        IAccountingPostingRepository postings,
        IJournalEntryRepository journalEntries,
        ICurrentUser user
    )
    {
        _postings = postings;
        _journalEntries = journalEntries;
        _user = user;
    }

    /// <returns>El asiento Posted a reversar, o null si no hay efecto publicado.</returns>
    public async Task<JournalEntry?> PrepareAsync(
        Guid tenantId,
        Guid companyId,
        string sourceModule,
        string factType,
        Guid sourceEventId,
        CancellationToken ct
    )
    {
        var posting = await _postings.LockByKeyAsync(
            tenantId,
            companyId,
            sourceModule,
            factType,
            sourceEventId,
            ct
        );

        var candidates = await _journalEntries.GetBySourceAsync(
            tenantId,
            companyId,
            sourceModule,
            sourceEventId,
            ct
        );
        var original = candidates.FirstOrDefault(x =>
            x.SourceEventType == factType && x.Status == JournalEntryStatus.Posted
        );

        if (original is not null)
        {
            // Asiento publicado cuyo resultado no llegó a la obligación: se enlaza, sin duplicar.
            if (posting is { IsResolved: false })
                posting.ResolveWithExistingEntry(original.Id, _user.UserId);
            return original;
        }

        if (posting is { IsResolved: false })
            posting.Cancel(_user.UserId);
        return null;
    }
}

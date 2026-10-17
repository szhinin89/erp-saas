using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>Estado contable de una apertura sujeta todavía no cerrada.</summary>
/// <param name="OpeningBalanceDate">Fecha de apertura (fecha contable de lotes y ASI).</param>
/// <param name="UnpostedBatches">Lotes de saldos Completed sin asiento Posted (sin estado, Pending o Failed).</param>
/// <param name="AsiMissing">No existe ASI vigente (el cierre de la carga lo exige siempre).</param>
/// <param name="AsiUnresolved">El ASI vigente existe pero no está Posted.</param>
public sealed record OpeningAccountingStatus(
    DateOnly OpeningBalanceDate,
    int UnpostedBatches,
    bool AsiMissing,
    bool AsiUnresolved
);

/// <summary>
/// ACCOUNTING-CONTINUITY-05/06 — única evaluación de la apertura contable de la Carga Inicial con su
/// modelo propio (OpeningBalancePosting/ASI), compartida por las precondiciones de cambio de modo y
/// el readiness de completitud/cierre. No aplica (null) sin fecha de apertura, si esa fecha no está
/// sujeta a una versión Enabled o si la Carga Inicial ya se cerró (accounting.md §10/§20).
/// </summary>
public sealed class OpeningAccountingCompleteness(
    IOpeningBalanceConstraintsReader openingBalance,
    IImportBatchRepository batches,
    IOpeningBalancePostingRepository openingPostings,
    IOpeningJournalEntryPostingRepository openingJournal)
{
    private static readonly ImportType[] BalanceImportTypes =
        [ImportType.InitialStock, ImportType.InitialReceivables, ImportType.InitialPayables];

    public async Task<OpeningAccountingStatus?> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<AccountingOperationalPolicy> versions,
        CancellationToken ct
    )
    {
        var openingDate = await openingBalance.GetOpeningBalanceDateAsync(ct);
        if (openingDate is not { } date
            || versions.FirstOrDefault(v => v.Covers(date))?.Mode != AccountingOperationalMode.Enabled)
            return null;
        if (await openingBalance.GetInitialLoadClosureAsync(ct) is not null)
            return null;

        var posted = (await openingPostings.ListByCompanyAsync(tenantId, companyId, ct))
            .Where(p => p.Status == OpeningBalancePostingStatus.Posted)
            .Select(p => p.ImportBatchId)
            .ToHashSet();
        var completed = await batches.ListAsync(tenantId, companyId, BalanceImportTypes, [ImportStatus.Completed], ct);
        var asi = await openingJournal.FindCurrentAsync(tenantId, companyId, ct);

        return new OpeningAccountingStatus(
            date,
            completed.Count(b => !posted.Contains(b.Id)),
            asi is null,
            asi is not null && asi.Status != OpeningBalancePostingStatus.Posted
        );
    }
}

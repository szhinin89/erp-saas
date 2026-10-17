using ERP.Application.Common.Services;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>Motivo de incompletitud contable y cantidad de elementos afectados (sin listar documentos).</summary>
public sealed record AccountingCompletenessReason(string Code, int Count);

/// <summary>
/// Completitud contable de una Company en un alcance (rango de reporte o período a cerrar), con fecha
/// de corte. <see cref="IsComplete"/> = sin ningún motivo.
/// </summary>
public sealed record AccountingCompleteness(
    bool IsComplete,
    DateOnly AsOf,
    IReadOnlyList<AccountingCompletenessReason> Reasons
);

/// <summary>Códigos estables de <see cref="AccountingCompletenessReason"/> (contrato API).</summary>
public static class AccountingCompletenessCodes
{
    public const string PostingsUnresolved = "POSTINGS_UNRESOLVED";
    public const string OpeningBatchesUnposted = "OPENING_BATCHES_UNPOSTED";
    public const string OpeningAsiMissing = "OPENING_ASI_MISSING";
    public const string OpeningAsiUnresolved = "OPENING_ASI_UNRESOLVED";
    public const string SaleCostPending = "SALE_COST_PENDING";
    public const string OriginsWithoutAccounting = "ORIGINS_WITHOUT_ACCOUNTING";

    // Solo cierre de período.
    public const string JournalEntriesNotFinal = "JOURNAL_ENTRIES_NOT_FINAL";
    public const string JournalEntriesWithoutNumber = "JOURNAL_ENTRIES_WITHOUT_NUMBER";
    public const string ReversalsIncomplete = "REVERSALS_INCOMPLETE";
    public const string PreviousPeriodOpen = "PREVIOUS_PERIOD_OPEN";
}

/// <summary>
/// ACCOUNTING-CONTINUITY-06 — readiness único de completitud contable (accounting.md §8/§9). La misma
/// evaluación alimenta el cierre de período, el pre-cierre y la advertencia de los reportes.
/// </summary>
public interface IAccountingCompletenessEvaluator
{
    /// <summary>
    /// Completitud de [<paramref name="from"/>, <paramref name="to"/>]; <paramref name="from"/> null =
    /// desde el inicio contable aplicable.
    /// </summary>
    Task<AccountingCompleteness> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? from,
        DateOnly to,
        CancellationToken ct
    );

    /// <summary>Readiness de cierre de <paramref name="period"/>: rango del período + asientos + período anterior Open.</summary>
    Task<AccountingCompleteness> EvaluateClosureAsync(
        Guid tenantId,
        Guid companyId,
        AccountingPeriod period,
        CancellationToken ct
    );
}

/// <summary>
/// Considera, dentro del alcance: obligaciones Pending/Failed; apertura sujeta no cerrada con lotes
/// sin asiento o ASI ausente/no publicado; costo de ventas pendiente de ventas sujetas; y documentos
/// sujetos sin obligación ni asiento. Los orígenes solo se concilian dentro de las ventanas Enabled y
/// desde la adopción de cada versión (su alta): nunca hechos previos a la adopción contable ni de
/// ventanas Disabled. El cierre agrega los asientos del período y el cierre secuencial.
/// </summary>
public sealed class AccountingCompletenessService(
    IAccountingOperationalPolicyRepository policies,
    ICompanyClock clock,
    IAccountingPostingRepository postings,
    OpeningAccountingCompleteness opening,
    IInventoryCostLedger costs,
    IAccountingRelevantFactsReader facts,
    IJournalEntryRepository journal,
    IAccountingPeriodRepository periods
) : IAccountingCompletenessEvaluator
{
    public async Task<AccountingCompleteness> EvaluateAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var reasons = await EvaluateRangeAsync(tenantId, companyId, from, to, ct);
        return Build(to, reasons);
    }

    public async Task<AccountingCompleteness> EvaluateClosureAsync(
        Guid tenantId,
        Guid companyId,
        AccountingPeriod period,
        CancellationToken ct
    )
    {
        var reasons = await EvaluateRangeAsync(tenantId, companyId, period.StartDate, period.EndDate, ct);

        var entries = await journal.GetClosureCountsAsync(tenantId, companyId, period.Id, ct);
        Add(reasons, AccountingCompletenessCodes.JournalEntriesNotFinal, entries.DraftOrNonFinalEntries);
        Add(reasons, AccountingCompletenessCodes.JournalEntriesWithoutNumber, entries.EntriesWithoutEntryNumber);
        Add(reasons, AccountingCompletenessCodes.ReversalsIncomplete, entries.IncompleteReversals);

        var previousOpen = (await periods.GetByCompanyAsync(tenantId, companyId, ct))
            .Count(p => p.Id != period.Id && p.EndDate < period.StartDate && p.Status == PeriodStatus.Open);
        Add(reasons, AccountingCompletenessCodes.PreviousPeriodOpen, previousOpen);

        return Build(period.EndDate, reasons);
    }

    /// <summary>Traducción al resumen que exige <c>AccountingPeriod.Close</c>.</summary>
    public static JournalEntryClosureReadiness ToClosureReadiness(AccountingCompleteness completeness)
    {
        bool Has(params string[] codes) => completeness.Reasons.Any(r => codes.Contains(r.Code));
        return new JournalEntryClosureReadiness(
            HasDraftOrNonFinalEntries: Has(AccountingCompletenessCodes.JournalEntriesNotFinal),
            HasEntriesWithoutEntryNumber: Has(AccountingCompletenessCodes.JournalEntriesWithoutNumber),
            HasIncompleteReversals: Has(AccountingCompletenessCodes.ReversalsIncomplete),
            HasUnresolvedPostings: Has(AccountingCompletenessCodes.PostingsUnresolved),
            HasUnresolvedOpening: Has(
                AccountingCompletenessCodes.OpeningBatchesUnposted,
                AccountingCompletenessCodes.OpeningAsiMissing,
                AccountingCompletenessCodes.OpeningAsiUnresolved
            ),
            HasPendingSaleCost: Has(AccountingCompletenessCodes.SaleCostPending),
            HasOriginsWithoutAccounting: Has(AccountingCompletenessCodes.OriginsWithoutAccounting),
            HasPreviousOpenPeriod: Has(AccountingCompletenessCodes.PreviousPeriodOpen)
        );
    }

    private async Task<List<AccountingCompletenessReason>> EvaluateRangeAsync(
        Guid tenantId,
        Guid companyId,
        DateOnly? from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var reasons = new List<AccountingCompletenessReason>();
        bool InRange(DateOnly d) => (from is null || d >= from) && d <= to;

        // Obligaciones comunes: solo existen para hechos sujetos (incluidos los reversos de la era Enabled).
        Add(reasons, AccountingCompletenessCodes.PostingsUnresolved,
            await postings.CountUnresolvedInRangeAsync(tenantId, companyId, from, to, ct));

        var versions = await policies.ListAsync(tenantId, companyId, ct);
        if (versions.Count == 0)
            return reasons;

        if (await opening.EvaluateAsync(tenantId, companyId, versions, ct) is { } status
            && InRange(status.OpeningBalanceDate))
        {
            Add(reasons, AccountingCompletenessCodes.OpeningBatchesUnposted, status.UnpostedBatches);
            Add(reasons, AccountingCompletenessCodes.OpeningAsiMissing, status.AsiMissing ? 1 : 0);
            Add(reasons, AccountingCompletenessCodes.OpeningAsiUnresolved, status.AsiUnresolved ? 1 : 0);
        }

        // Costo pendiente: la sujeción se ancla a la emisión de la factura (accounting.md §20).
        var pendingCost = (await costs.GetPendingObligationInvoiceDatesAsync(tenantId, companyId, ct))
            .Count(d => InRange(d) && IsSubject(versions, d));
        Add(reasons, AccountingCompletenessCodes.SaleCostPending, pendingCost);

        var withoutAccounting = 0;
        foreach (var version in versions.Where(v => v.Mode == AccountingOperationalMode.Enabled))
        {
            var adoptedOn = await clock.LocalDateAsync(companyId, tenantId, version.CreatedAt, ct);
            var start = Max(Max(version.EffectiveFrom, adoptedOn), from ?? DateOnly.MinValue);
            var end = Min(version.EffectiveTo ?? DateOnly.MaxValue, to);
            if (start <= end)
                withoutAccounting += await facts.CountWithoutAccountingAsync(tenantId, companyId, start, end, ct);
        }
        Add(reasons, AccountingCompletenessCodes.OriginsWithoutAccounting, withoutAccounting);

        return reasons;
    }

    private static bool IsSubject(IReadOnlyList<AccountingOperationalPolicy> versions, DateOnly date) =>
        versions.FirstOrDefault(v => v.Covers(date))?.Mode == AccountingOperationalMode.Enabled;

    private static void Add(List<AccountingCompletenessReason> reasons, string code, int count)
    {
        if (count > 0)
            reasons.Add(new AccountingCompletenessReason(code, count));
    }

    private static AccountingCompleteness Build(DateOnly asOf, List<AccountingCompletenessReason> reasons) =>
        new(reasons.Count == 0, asOf, reasons);

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}

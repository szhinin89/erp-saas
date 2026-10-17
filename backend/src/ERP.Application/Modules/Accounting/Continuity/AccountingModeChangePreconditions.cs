using ERP.Application.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-05 — precondiciones de una versión nueva del modo operativo
/// (accounting.md §3). Se evalúan bajo el lock exclusivo de política de la Company, que serializa con
/// el registro de obligaciones, el cierre de período y el cambio de <c>IsAccountingReq</c>.
/// Una vigencia nunca puede alcanzar retroactivamente operaciones ya ocurridas.
/// </summary>
public sealed class AccountingModeChangePreconditions(
    IAccountingPostingRepository postings,
    IInventoryCostLedger costs,
    IOpeningBalanceConstraintsReader openingBalance,
    IImportBatchRepository batches,
    IOpeningBalancePostingRepository openingPostings,
    IOpeningJournalEntryPostingRepository openingJournal,
    IAccountingRelevantFactsReader facts)
{
    private static readonly ImportType[] BalanceImportTypes =
        [ImportType.InitialStock, ImportType.InitialReceivables, ImportType.InitialPayables];

    /// <summary>Motivo de rechazo, o null si la versión nueva es válida.</summary>
    public async Task<string?> CheckAsync(
        Guid tenantId,
        Guid companyId,
        bool isAccountingRequired,
        IReadOnlyList<AccountingOperationalPolicy> versions,
        AccountingOperationalMode mode,
        DateOnly effectiveFrom,
        DateOnly today,
        CancellationToken ct
    )
    {
        if (mode == AccountingOperationalMode.Enabled)
        {
            // Activar nunca vuelve sujetos hechos ya ocurridos: una vigencia pasada solo procede si
            // no existe ningún hecho contablemente relevante desde esa fecha (y nunca se generan
            // obligaciones históricas). Con versiones previas, la vigencia pasada se rechaza siempre.
            if (effectiveFrom >= today)
                return null;
            if (versions.Count > 0)
                return $"La activación de Contabilidad no puede ser retroactiva: use una fecha desde el {today:yyyy-MM-dd}.";
            return await facts.ExistsOnOrAfterAsync(tenantId, companyId, effectiveFrom, ct)
                ? $"Existen operaciones desde el {effectiveFrom:yyyy-MM-dd} que quedarían sujetas retroactivamente: "
                    + $"use una fecha posterior a esas operaciones o desde el {today:yyyy-MM-dd}."
                : null;
        }

        if (isAccountingRequired)
            return "La empresa está obligada a llevar contabilidad: Contabilidad no puede deshabilitarse.";
        if (effectiveFrom < today)
            return $"La desactivación de Contabilidad no puede ser retroactiva: use una fecha desde el {today:yyyy-MM-dd}.";

        var latest = await postings.GetLatestAutomaticEntryDateAsync(tenantId, companyId, ct);
        if (latest is { } last && effectiveFrom <= last)
            return $"Existen contabilizaciones con fecha {last:yyyy-MM-dd}: la desactivación debe iniciar después de esa fecha.";

        if (await postings.HasUnresolvedAsync(tenantId, companyId, ct))
            return "Existen contabilizaciones pendientes o fallidas: resuélvalas antes de deshabilitar Contabilidad.";

        var pendingCostDates = await costs.GetPendingObligationInvoiceDatesAsync(tenantId, companyId, ct);
        if (pendingCostDates.Any(d => IsSubject(versions, d)))
            return "Existen ventas sujetas a Contabilidad con costo de ventas pendiente de resolver: "
                + "regularice su costo antes de deshabilitar Contabilidad.";

        if (await HasUnresolvedOpeningAsync(tenantId, companyId, versions, ct))
            return "La apertura contable de la carga inicial está sin resolver (lotes sin asiento o ASI pendiente/fallido): "
                + "resuélvala antes de deshabilitar Contabilidad.";

        return null;
    }

    private static bool IsSubject(IReadOnlyList<AccountingOperationalPolicy> versions, DateOnly date) =>
        versions.FirstOrDefault(v => v.Covers(date))?.Mode == AccountingOperationalMode.Enabled;

    private async Task<bool> HasUnresolvedOpeningAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<AccountingOperationalPolicy> versions,
        CancellationToken ct
    )
    {
        var openingDate = await openingBalance.GetOpeningBalanceDateAsync(ct);
        if (openingDate is not { } date || !IsSubject(versions, date))
            return false;
        if (await openingBalance.GetInitialLoadClosureAsync(ct) is not null)
            return false;

        var posted = (await openingPostings.ListByCompanyAsync(tenantId, companyId, ct))
            .Where(p => p.Status == OpeningBalancePostingStatus.Posted)
            .Select(p => p.ImportBatchId)
            .ToHashSet();
        var completed = await batches.ListAsync(tenantId, companyId, BalanceImportTypes, [ImportStatus.Completed], ct);
        if (completed.Any(b => !posted.Contains(b.Id)))
            return true;

        var asi = await openingJournal.FindCurrentAsync(tenantId, companyId, ct);
        return asi is not null && asi.Status != OpeningBalancePostingStatus.Posted;
    }
}

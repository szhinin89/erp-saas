using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
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
    OpeningAccountingCompleteness opening,
    IAccountingRelevantFactsReader facts)
{
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
            // ACCOUNTING-FINAL-FIX: la primera activación debe cubrir la fecha de apertura (en curso o ya
            // cerrada); si no, la apertura quedaría no sujeta para siempre mientras las operaciones
            // posteriores sí se contabilizan. Con versiones previas la activación nunca es retroactiva.
            if (versions.Count == 0
                && await opening.GetOpeningBalanceDateAsync(ct) is { } openingDate
                && effectiveFrom > openingDate)
                return $"La empresa tiene fecha de apertura {openingDate:yyyy-MM-dd}: la primera activación de "
                    + "Contabilidad debe regir desde esa fecha o antes, para que los saldos iniciales de la Carga "
                    + $"Inicial generen su asiento de apertura. Use como vigencia el {openingDate:yyyy-MM-dd} o una fecha anterior.";
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

    // El ASI ausente no impide deshabilitar (la carga sigue abierta y su cierre lo exigirá); sí lo
    // impiden los lotes sin asiento y un ASI vigente no publicado.
    private async Task<bool> HasUnresolvedOpeningAsync(
        Guid tenantId,
        Guid companyId,
        IReadOnlyList<AccountingOperationalPolicy> versions,
        CancellationToken ct
    ) =>
        await opening.EvaluateAsync(tenantId, companyId, versions, ct) is { } status
        && (status.UnpostedBatches > 0 || status.AsiUnresolved);
}

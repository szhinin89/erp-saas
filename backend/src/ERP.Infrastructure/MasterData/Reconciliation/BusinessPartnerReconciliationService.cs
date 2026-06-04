using ERP.Application.Common;
using ERP.Application.MasterData.Reconciliation;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Reconciliation;

/// <summary>
/// READ-ONLY: detecta problemas de integridad en el modelo BusinessPartner V2.
///
/// ELIMINADO (legacy): DetectOrphanProfilesAsync — CustomerProfile/SupplierProfile ya no existen.
/// ELIMINADO (legacy): DetectLegacyWithoutProfilesAsync — concepto legacy eliminado.
///
/// VIGENTE: DetectDuplicateIdentificationsAsync — sigue siendo relevante en V2.
/// </summary>
public sealed class BusinessPartnerReconciliationService : IMasterDataReconciliationService
{
    private readonly IPlatformQueryAccessor _platform;
    private readonly ErpDbContext           _db;

    public BusinessPartnerReconciliationService(IPlatformQueryAccessor platform, ErpDbContext db)
        => (_platform, _db) = (platform, db);

    public async Task<MasterDataReconciliationReport> AnalyzeAsync(CancellationToken ct = default)
    {
        var issues = new List<MasterDataReconciliationIssue>();

        await foreach (var issue in DetectDuplicateIdentificationsAsync(ct))
            issues.Add(issue);

        await foreach (var issue in DetectOrphanRolesAsync(ct))
            issues.Add(issue);

        return new MasterDataReconciliationReport(issues.Count == 0, issues.Count, issues);
    }

    /// <summary>Detecta identificaciones fiscales duplicadas dentro del mismo subscriber.</summary>
    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectDuplicateIdentificationsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var dupes = await _platform
            .Unfiltered(_db.BusinessPartners.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
            .GroupBy(b => new { b.SubscriberId, b.Identification.Type, b.Identification.Number })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.SubscriberId)
            .Distinct()
            .Take(20)
            .ToListAsync(ct);

        foreach (var subId in dupes)
            yield return new MasterDataReconciliationIssue(
                "duplicate_bp_identification",
                "critical",
                "Identificación fiscal duplicada para el mismo subscriber.",
                subId);
    }

    /// <summary>Detecta BusinessPartnerRoles sin un BusinessPartner padre válido.</summary>
    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectOrphanRolesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var orphanSubIds = await _platform
            .Unfiltered(_db.BusinessPartnerRoles.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
            .Where(r => !_db.BusinessPartners.Any(b => b.Id == r.BusinessPartnerId))
            .Select(r => r.SubscriberId)
            .Distinct()
            .Take(20)
            .ToListAsync(ct);

        foreach (var subId in orphanSubIds)
            yield return new MasterDataReconciliationIssue(
                "orphan_bp_role",
                "warning",
                "BusinessPartnerRole sin BusinessPartner padre.",
                subId);
    }
}

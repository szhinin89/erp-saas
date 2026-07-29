using ERP.Application.MasterData.Reconciliation;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Reconciliation;

/// <summary>
/// READ-ONLY: detecta problemas de integridad en el modelo BusinessPartner V2.
/// </summary>
public sealed class BusinessPartnerReconciliationService : IMasterDataReconciliationService
{
    private readonly ErpDbContext _db;

    public BusinessPartnerReconciliationService(ErpDbContext db) => _db = db;

    public async Task<MasterDataReconciliationReport> AnalyzeAsync(
        CancellationToken cancellationToken = default
    )
    {
        var issues = new List<MasterDataReconciliationIssue>();

        await foreach (var issue in DetectDuplicateIdentificationsAsync(cancellationToken))
            issues.Add(issue);

        await foreach (var issue in DetectOrphanRolesAsync(cancellationToken))
            issues.Add(issue);

        return new MasterDataReconciliationReport(issues.Count == 0, issues.Count, issues);
    }

    /// <summary>Detecta identificaciones fiscales duplicadas dentro del mismo tenant.</summary>
    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectDuplicateIdentificationsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var dupes = await _db
            .BusinessPartners.IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(b => new
            {
                b.TenantId,
                b.Identification.Type,
                b.Identification.Number,
            })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.TenantId)
            .Distinct()
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var subId in dupes)
            yield return new MasterDataReconciliationIssue(
                "duplicate_bp_identification",
                "critical",
                "Identificación fiscal duplicada para el mismo tenant.",
                subId
            );
    }

    /// <summary>Detecta BusinessPartnerRoles sin un BusinessPartner padre válido.</summary>
    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectOrphanRolesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var orphanSubIds = await _db
            .BusinessPartnerRoles.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => !_db.BusinessPartners.Any(b => b.Id == r.BusinessPartnerId))
            .Select(r => r.TenantId)
            .Distinct()
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var subId in orphanSubIds)
            yield return new MasterDataReconciliationIssue(
                "orphan_bp_role",
                "warning",
                "BusinessPartnerRole sin BusinessPartner padre.",
                subId
            );
    }
}

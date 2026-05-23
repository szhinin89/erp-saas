using ERP.Application.Common;
using ERP.Application.MasterData.Reconciliation;
using ERP.Domain.MasterData.ValueObjects;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.MasterData.Reconciliation;

/// <summary>READ-ONLY: detecta drift entre entidades legacy y MasterData BC.</summary>
public sealed class BusinessPartnerReconciliationService : IMasterDataReconciliationService
{
    private readonly IPlatformQueryAccessor _platform;
    private readonly ErpDbContext _db;

    public BusinessPartnerReconciliationService(
        IPlatformQueryAccessor platform,
        ErpDbContext db)
    {
        _platform = platform;
        _db = db;
    }

    public async Task<MasterDataReconciliationReport> AnalyzeAsync(CancellationToken ct = default)
    {
        var issues = new List<MasterDataReconciliationIssue>();

        await foreach (var issue in DetectOrphanProfilesAsync(ct))
            issues.Add(issue);

        await foreach (var issue in DetectLegacyWithoutProfilesAsync(ct))
            issues.Add(issue);

        await foreach (var issue in DetectDuplicateIdentificationsAsync(ct))
            issues.Add(issue);

        return new MasterDataReconciliationReport(
            issues.Count == 0,
            issues.Count,
            issues);
    }

    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectOrphanProfilesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var orphanCustomers = await _platform.Unfiltered(
            _db.CustomerProfiles.Where(p => !_db.BusinessPartners.Any(b => b.Id == p.BusinessPartnerId)),
            PlatformQueryReason.PlatformMetrics)
            .Take(20).Select(p => p.SubscriberId).ToListAsync(ct);

        foreach (var subId in orphanCustomers.Distinct())
        {
            yield return new MasterDataReconciliationIssue(
                "orphan_customer_profile",
                "warning",
                "CustomerProfile sin BusinessPartner asociado.",
                subId);
        }

        var orphanSuppliers = await _platform.Unfiltered(
            _db.SupplierProfiles.Where(p => !_db.BusinessPartners.Any(b => b.Id == p.BusinessPartnerId)),
            PlatformQueryReason.PlatformMetrics)
            .Take(20).Select(p => p.SubscriberId).ToListAsync(ct);

        foreach (var subId in orphanSuppliers.Distinct())
        {
            yield return new MasterDataReconciliationIssue(
                "orphan_supplier_profile",
                "warning",
                "SupplierProfile sin BusinessPartner asociado.",
                subId);
        }
    }

    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectLegacyWithoutProfilesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var customers = await _platform.Unfiltered(_db.Customers.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
            .Where(c => c.IsActive)
            .Take(500)
            .ToListAsync(ct);

        foreach (var c in customers)
        {
            var idType = c.IdentificationType;
            var idNum = c.IdentificationNumber;
            var bp = await _platform.Unfiltered(_db.BusinessPartners.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
                .FirstOrDefaultAsync(b =>
                    b.SubscriberId == c.SubscriberId &&
                    b.Identification.Type == idType &&
                    b.Identification.Number == idNum, ct);

            if (bp is null)
            {
                yield return new MasterDataReconciliationIssue(
                    "legacy_customer_without_bp",
                    "info",
                    $"Customer activo sin BusinessPartner ({idType} {idNum}).",
                    c.SubscriberId,
                    c.Id);
                continue;
            }

            var hasProfile = await _platform.Unfiltered(_db.CustomerProfiles.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
                .AnyAsync(p => p.BusinessPartnerId == bp.Id, ct);

            if (!hasProfile)
            {
                yield return new MasterDataReconciliationIssue(
                    "customer_without_profile",
                    "warning",
                    "BusinessPartner existe pero falta CustomerProfile.",
                    c.SubscriberId,
                    bp.Id);
            }
        }

        var suppliers = await _platform.Unfiltered(_db.Suppliers.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
            .Where(s => s.IsActive)
            .Take(500)
            .ToListAsync(ct);

        foreach (var s in suppliers)
        {
            var idType = s.PersonType == Supplier.TypeLegal
                ? TaxIdentification.TypeRuc
                : TaxIdentification.TypeCi;

            var bp = await _platform.Unfiltered(_db.BusinessPartners.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
                .FirstOrDefaultAsync(b =>
                    b.SubscriberId == s.SubscriberId &&
                    b.Identification.Type == idType &&
                    b.Identification.Number == s.Ruc, ct);

            if (bp is null)
            {
                yield return new MasterDataReconciliationIssue(
                    "legacy_supplier_without_bp",
                    "info",
                    $"Supplier activo sin BusinessPartner ({s.Ruc}).",
                    s.SubscriberId,
                    s.Id);
                continue;
            }

            var hasProfile = await _platform.Unfiltered(_db.SupplierProfiles.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
                .AnyAsync(p => p.BusinessPartnerId == bp.Id, ct);

            if (!hasProfile)
            {
                yield return new MasterDataReconciliationIssue(
                    "supplier_without_profile",
                    "warning",
                    "BusinessPartner existe pero falta SupplierProfile.",
                    s.SubscriberId,
                    bp.Id);
            }
        }
    }

    private async IAsyncEnumerable<MasterDataReconciliationIssue> DetectDuplicateIdentificationsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var dupes = await _platform.Unfiltered(_db.BusinessPartners.AsNoTracking(), PlatformQueryReason.PlatformMetrics)
            .GroupBy(b => new { b.SubscriberId, b.Identification.Type, b.Identification.Number })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.SubscriberId)
            .Distinct()
            .Take(20)
            .ToListAsync(ct);

        foreach (var subId in dupes)
        {
            yield return new MasterDataReconciliationIssue(
                "duplicate_bp_identification",
                "critical",
                "Identificación duplicada en BusinessPartners del subscriber.",
                subId);
        }
    }
}

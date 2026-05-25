using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Billing.Entities;
using ERP.Domain.Billing.Enums;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class SubscriberIntegrityRepairService : ISubscriberIntegrityRepairService
{
    private readonly ErpDbContext _db;
    private readonly ICompanyProvisioningService _companyProvisioning;
    private readonly IPlatformQueryAccessor _platform;

    public SubscriberIntegrityRepairService(
        ErpDbContext db,
        ICompanyProvisioningService companyProvisioning,
        IPlatformQueryAccessor platform)
    {
        _db = db;
        _companyProvisioning = companyProvisioning;
        _platform = platform;
    }

    public async Task<EnterpriseIntegrityReport> ScanAsync(CancellationToken ct = default)
    {
        var issues = new List<EnterpriseIntegrityIssue>();
        issues.AddRange(await ScanSubscribersWithoutCompanyAsync(ct));
        issues.AddRange(await ScanCompaniesWithoutAdminMembershipAsync(ct));
        issues.AddRange(await ScanSubscribersWithoutBillingAsync(ct));
        return new EnterpriseIntegrityReport(issues, 0);
    }

    public async Task<EnterpriseIntegrityReport> RepairAsync(CancellationToken ct = default)
    {
        var issues = new List<EnterpriseIntegrityIssue>();
        var repaired = 0;

        var subscribersWithoutCompany = await _platform
            .Unfiltered(_db.Subscribers, PlatformQueryReason.CrossSubscriberSystem)
            .Select(s => s.Id)
            .ToListAsync(ct);

        foreach (var subscriberId in subscribersWithoutCompany)
        {
            var hasCompany = await _db.Companies.AnyAsync(c => c.SubscriberId == subscriberId, ct);
            if (hasCompany)
                continue;

            var subscriber = await _db.Subscribers.FirstOrDefaultAsync(s => s.Id == subscriberId, ct);
            if (subscriber is null)
                continue;

            issues.Add(new EnterpriseIntegrityIssue(
                "subscriber.without_company",
                $"Subscriber '{subscriber.Slug}' sin company operativa.",
                SubscriberId: subscriberId));

            var company = await _companyProvisioning.CreateDefaultCompanyForSubscriberAsync(subscriber, ct: ct);
            _db.Companies.Add(company);
            await _db.SaveChangesAsync(ct);
            repaired++;
        }

        issues.AddRange(await ScanCompaniesWithoutAdminMembershipAsync(ct));
        issues.AddRange(await ScanSubscribersWithoutBillingAsync(ct));

        foreach (var billingIssue in issues.Where(i => i.Code == "subscriber.without_billing").ToList())
        {
            if (!billingIssue.SubscriberId.HasValue)
                continue;

            var subscriber = await _db.Subscribers.FirstOrDefaultAsync(s => s.Id == billingIssue.SubscriberId, ct);
            if (subscriber is null)
                continue;

            var adminEmail = await _db.CompanyUserMemberships
                .Where(m => m.IsActive)
                .Join(_db.Companies.Where(c => c.SubscriberId == subscriber.Id), m => m.CompanyId, c => c.Id, (m, c) => m)
                .Join(_db.IdentityUsers, m => m.IdentityUserId, u => u.Id, (_, u) => u.Email.Value)
                .FirstOrDefaultAsync(ct) ?? "billing@repair.local";

            _db.SubscriberBillingAccounts.Add(SubscriberBillingAccount.Create(subscriber.Id, adminEmail, Guid.Empty));
            await _db.SaveChangesAsync(ct);
            repaired++;
        }

        return new EnterpriseIntegrityReport(issues, repaired);
    }

    private async Task<IReadOnlyList<EnterpriseIntegrityIssue>> ScanSubscribersWithoutCompanyAsync(CancellationToken ct)
    {
        var subscriberIds = await _platform
            .Unfiltered(_db.Subscribers, PlatformQueryReason.CrossSubscriberSystem)
            .Select(s => new { s.Id, s.Slug })
            .ToListAsync(ct);

        var issues = new List<EnterpriseIntegrityIssue>();
        foreach (var s in subscriberIds)
        {
            var hasCompany = await _db.Companies.AnyAsync(c => c.SubscriberId == s.Id, ct);
            if (!hasCompany)
            {
                issues.Add(new EnterpriseIntegrityIssue(
                    "subscriber.without_company",
                    $"Subscriber '{s.Slug}' no tiene fila en company.",
                    SubscriberId: s.Id));
            }
        }

        return issues;
    }

    private async Task<IReadOnlyList<EnterpriseIntegrityIssue>> ScanCompaniesWithoutAdminMembershipAsync(CancellationToken ct)
    {
        var companies = await _db.Companies.AsNoTracking()
            .Select(c => new { c.Id, c.SubscriberId, c.LegalName })
            .ToListAsync(ct);

        var issues = new List<EnterpriseIntegrityIssue>();
        foreach (var company in companies)
        {
            var hasAdmin = await _db.CompanyUserMemberships.AnyAsync(
                m => m.CompanyId == company.Id && m.IsActive && m.Role == "Admin",
                ct);
            if (!hasAdmin)
            {
                issues.Add(new EnterpriseIntegrityIssue(
                    "company.without_admin_membership",
                    $"Company '{company.LegalName}' sin membership Admin activa.",
                    SubscriberId: company.SubscriberId,
                    CompanyId: company.Id));
            }
        }

        return issues;
    }

    private async Task<IReadOnlyList<EnterpriseIntegrityIssue>> ScanSubscribersWithoutBillingAsync(CancellationToken ct)
    {
        var subscriberIds = await _platform
            .Unfiltered(_db.Subscribers, PlatformQueryReason.CrossSubscriberSystem)
            .Select(s => new { s.Id, s.Slug })
            .ToListAsync(ct);

        var issues = new List<EnterpriseIntegrityIssue>();
        foreach (var s in subscriberIds)
        {
            var hasBilling = await _db.SubscriberBillingAccounts.AnyAsync(b => b.SubscriberId == s.Id, ct);
            if (!hasBilling)
            {
                issues.Add(new EnterpriseIntegrityIssue(
                    "subscriber.without_billing",
                    $"Subscriber '{s.Slug}' sin cuenta de billing.",
                    SubscriberId: s.Id));
            }
        }

        return issues;
    }
}

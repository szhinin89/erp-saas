using ERP.Application.Common;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscriptions.Entities;

namespace ERP.Infrastructure.Services;

public sealed class CompanyProvisioningService : ICompanyProvisioningService
{
    private readonly ICompanyRepository _companies;
    private readonly ICommercialPlanLimitService _planLimits;
    private readonly IAccessRepository _access;

    public CompanyProvisioningService(
        ICompanyRepository companies,
        ICommercialPlanLimitService planLimits,
        IAccessRepository access)
    {
        _companies = companies;
        _planLimits = planLimits;
        _access = access;
    }

    public async Task<Company> EnsureDefaultCompanyAsync(Subscriber subscriber, CancellationToken ct = default)
    {
        var active = await _companies.GetActiveBySubscriberIdAsync(subscriber.Id, ct);
        if (active.Count > 0)
            return active[0];

        var company = await CreateDefaultCompanyForSubscriberAsync(subscriber, ct: ct);

        Company? created = null;
        await _planLimits.ExecuteWithLimitEnforcementAsync(
            subscriber.Id,
            CommercialPlanLimit.Codes.MaxCompanies,
            increment: 1,
            async innerCt =>
            {
                await _companies.AddAsync(company, innerCt);
                created = company;
            },
            ct);

        return created ?? company;
    }

    public async Task<Company> CreateDefaultCompanyForSubscriberAsync(
        Subscriber subscriber,
        string? countryCode = "ECU",
        string? timezone = "America/Guayaquil",
        CancellationToken ct = default)
    {
        var (taxId, isProvisional, status) = await ResolveTaxIdAsync(subscriber.Ruc, ct);

        return Company.CreateManaged(
            subscriber.Id,
            taxId,
            legalName: subscriber.Name,
            mainAddress: "—",
            tradeName: subscriber.TradeName ?? subscriber.ShortName,
            email: null,
            phone: null,
            countryCode: string.IsNullOrWhiteSpace(countryCode) ? "ECU" : countryCode,
            timezone: string.IsNullOrWhiteSpace(timezone) ? "America/Guayaquil" : timezone,
            currencyCode: "USD",
            isProvisionalTaxId: isProvisional,
            taxIdStatus: status);
    }

    public async Task<Company> CreateManagedCompanyAsync(
        Guid subscriberId,
        string ruc,
        string legalName,
        string mainAddress,
        Guid createdByUserId,
        string creatorRole,
        string? tradeName = null,
        string? email = null,
        string? phone = null,
        string countryCode = "ECU",
        string timezone = "America/Guayaquil",
        string currencyCode = "USD",
        string? logoUrl = null,
        string? brandingJson = null,
        CancellationToken ct = default)
    {
        var normalized = NormalizeProvidedTaxId(ruc);
        await EnsureTaxIdAvailableGloballyAsync(normalized, ct);

        var company = Company.CreateManaged(
            subscriberId,
            normalized,
            legalName,
            mainAddress,
            tradeName,
            email,
            phone,
            countryCode,
            timezone,
            currencyCode,
            logoUrl,
            brandingJson,
            isProvisionalTaxId: ProvisionalTaxIdGenerator.IsProvisional(normalized),
            taxIdStatus: ProvisionalTaxIdGenerator.IsProvisional(normalized)
                ? TaxIdStatus.Pending
                : TaxIdStatus.Verified);

        Company? created = null;
        await _planLimits.ExecuteWithLimitEnforcementAsync(
            subscriberId,
            CommercialPlanLimit.Codes.MaxCompanies,
            increment: 1,
            async innerCt =>
            {
                await _companies.AddAsync(company, innerCt);
                await _companies.SaveChangesAsync(innerCt);

                var membership = CompanyUserMembership.Create(
                    company.Id,
                    createdByUserId,
                    creatorRole,
                    profileId: null,
                    createdByUserId);
                await _access.AddCompanyUserMembershipAsync(membership, innerCt);
                await _access.SaveChangesAsync(innerCt);
                created = company;
            },
            ct);

        return created ?? company;
    }

    private async Task<(string TaxId, bool IsProvisional, TaxIdStatus Status)> ResolveTaxIdAsync(
        string? providedRuc,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(providedRuc))
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var provisional = ProvisionalTaxIdGenerator.Create();
                if (await _companies.GetByRucAsync(provisional, ct) is null)
                    return (provisional, true, TaxIdStatus.Pending);
            }

            throw new InvalidOperationException("No se pudo generar un RUC provisional único.");
        }

        var normalized = NormalizeProvidedTaxId(providedRuc);
        await EnsureTaxIdAvailableGloballyAsync(normalized, ct);
        return (normalized, false, TaxIdStatus.Verified);
    }

    private static string NormalizeProvidedTaxId(string ruc) =>
        ProvisionalTaxIdGenerator.IsProvisional(ruc)
            ? ruc.Trim()
            : ProvisionalTaxIdGenerator.NormalizeOfficial(ruc);

    private async Task EnsureTaxIdAvailableGloballyAsync(string taxId, CancellationToken ct)
    {
        var existing = await _companies.GetByRucAsync(taxId, ct);
        if (existing is not null)
            throw new CompanyRucAlreadyExistsException(taxId);
    }
}

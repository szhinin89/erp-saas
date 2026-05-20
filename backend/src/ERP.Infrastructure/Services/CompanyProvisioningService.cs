using ERP.Application.Common;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Entities;
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

        var ruc = string.IsNullOrWhiteSpace(subscriber.Ruc)
            ? "0000000000000"
            : subscriber.Ruc.Trim();

        if (ruc.Length != 13)
            ruc = ruc.PadRight(13, '0')[..13];

        var existingByRuc = await _companies.GetBySubscriberAndRucAsync(subscriber.Id, ruc, ct);
        if (existingByRuc is not null)
            return existingByRuc;

        var company = Company.CreateFromSubscriber(
            subscriber.Id,
            ruc,
            legalName: subscriber.Name,
            mainAddress: "—",
            tradeName: subscriber.TradeName ?? subscriber.ShortName,
            email: null,
            phone: null);

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
        var existingGlobal = await _companies.GetByRucAsync(ruc, ct);
        if (existingGlobal is not null)
            throw new InvalidOperationException("El RUC ya está registrado en el sistema.");

        var existingInSub = await _companies.GetBySubscriberAndRucAsync(subscriberId, ruc, ct);
        if (existingInSub is not null)
            throw new InvalidOperationException("Ya existe una empresa con este RUC en el suscriptor.");

        var company = Company.CreateManaged(
            subscriberId,
            ruc,
            legalName,
            mainAddress,
            tradeName,
            email,
            phone,
            countryCode,
            timezone,
            currencyCode,
            logoUrl,
            brandingJson);

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
}

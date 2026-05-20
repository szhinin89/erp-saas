using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscriptions.Entities;

namespace ERP.Infrastructure.Services.CommercialLimitUsage;

public sealed class MaxCompaniesLimitUsageProvider : ICommercialLimitUsageProvider
{
    private readonly ICompanyRepository _companies;

    public MaxCompaniesLimitUsageProvider(ICompanyRepository companies)
    {
        _companies = companies;
    }

    public bool Supports(string limitCode) =>
        string.Equals(limitCode, CommercialPlanLimit.Codes.MaxCompanies, StringComparison.OrdinalIgnoreCase);

    public async Task<long> GetCurrentUsageAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var count = await _companies.CountActiveBySubscriberIdAsync(subscriberId, ct);
        return count;
    }
}

using ERP.Application.Common;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CompanyContextResolver : ICompanyContextResolver
{
    private readonly ICompanyRepository _companies;

    public CompanyContextResolver(ICompanyRepository companies)
    {
        _companies = companies;
    }

    public async Task<Guid?> ResolveDefaultCompanyIdAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return null;

        var active = await _companies.GetActiveBySubscriberIdAsync(subscriberId, ct);
        if (active.Count == 0)
            return null;
        if (active.Count == 1)
            return active[0].Id;

        // Multiempresa: hasta Fase 2 UI, la primera activa por nombre (determinista).
        return active[0].Id;
    }

    public Task<int> CountActiveCompaniesAsync(Guid subscriberId, CancellationToken ct = default)
        => _companies.CountActiveBySubscriberIdAsync(subscriberId, ct);
}

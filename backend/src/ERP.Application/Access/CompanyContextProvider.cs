using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Access;

public sealed class CompanyContextProvider : ICompanyContextProvider
{
    private readonly IAccessRepository _repo;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CompanyContextProvider(
        IAccessRepository repo,
        ICompanyRepository companyRepository,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser)
    {
        _repo = repo;
        _companyRepository = companyRepository;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Guid?> ResolveDefaultCompanyIdAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return null;

        var active = await _companyRepository.GetActiveBySubscriberIdAsync(subscriberId, ct);
        if (active.Count == 0)
            return null;
        if (active.Count == 1)
            return active[0].Id;

        // Multiempresa: hasta Fase 2 UI, la primera activa por nombre (determinista).
        return active[0].Id;
    }

    public Task<int> CountActiveCompaniesAsync(Guid subscriberId, CancellationToken ct = default)
        => _companyRepository.CountActiveBySubscriberIdAsync(subscriberId, ct);

    public Task<OperationalCompanyContext?> ResolveOperationalForCurrentUserAsync(CancellationToken ct = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Task.FromResult<OperationalCompanyContext?>(null);

        return ResolveOperationalForUserAsync(_currentUser.UserId, ct);
    }

    public async Task<OperationalCompanyContext?> ResolveOperationalForUserAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || !_currentSubscriber.IsAuthenticated)
            return null;

        var companyId = await ResolveOperationalCompanyIdAsync(userId, ct);
        if (companyId == Guid.Empty)
            return null;

        var membership = await _repo.GetCompanyUserMembershipAsync(companyId, userId, ct);
        if (membership is null)
            return new OperationalCompanyContext(companyId, userId, null, IsActiveMembership: false);

        return new OperationalCompanyContext(
            companyId,
            userId,
            membership.ProfileId,
            membership.IsActive);
    }

    private async Task<Guid> ResolveOperationalCompanyIdAsync(Guid userId, CancellationToken ct)
    {
        if (_currentCompany.HasCompanyContext && _currentCompany.CompanyId != Guid.Empty)
            return _currentCompany.CompanyId;

        var memberships = await _repo.GetActiveCompanyUserMembershipsForUserSystemAsync(userId, ct);
        var companies = await _companyRepository.GetByIdsAsync(memberships.Select(m => m.CompanyId).ToList(), ct);
        var inSubscriber = companies.Where(c => c.SubscriberId == _currentSubscriber.SubscriberId).ToList();
        return inSubscriber.Count == 1 ? inSubscriber[0].Id : Guid.Empty;
    }
}

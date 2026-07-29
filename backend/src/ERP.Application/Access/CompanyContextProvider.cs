using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Access;

public sealed class CompanyContextProvider : ICompanyContextProvider
{
    private readonly IAccessRepository _repo;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CompanyContextProvider(
        IAccessRepository repo,
        ICompanyRepository companyRepository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser
    )
    {
        _repo = repo;
        _companyRepository = companyRepository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
    }

    public async Task<Guid?> ResolveDefaultCompanyIdAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        if (tenantId == Guid.Empty)
            return null;

        var active = await _companyRepository.GetActiveByTenantIdAsync(tenantId, cancellationToken);
        if (active.Count == 0)
            return null;
        if (active.Count == 1)
            return active[0].Id;

        // Multiempresa: hasta Fase 2 UI, la primera activa por nombre (determinista).
        return active[0].Id;
    }

    public Task<int> CountActiveCompaniesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    ) => _companyRepository.CountActiveByTenantIdAsync(tenantId, cancellationToken);

    public Task<OperationalCompanyContext?> ResolveOperationalForCurrentUserAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            return Task.FromResult<OperationalCompanyContext?>(null);

        return ResolveOperationalForUserAsync(_currentUser.UserId, cancellationToken);
    }

    public async Task<OperationalCompanyContext?> ResolveOperationalForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default
    )
    {
        if (userId == Guid.Empty || !(_currentTenant.TenantId != Guid.Empty))
            return null;

        var companyId = await ResolveOperationalCompanyIdAsync(userId, cancellationToken);
        if (companyId == Guid.Empty)
            return null;

        var membership = await _repo.GetCompanyUserMembershipAsync(
            companyId,
            userId,
            cancellationToken
        );
        if (membership is null)
            return new OperationalCompanyContext(
                companyId,
                userId,
                null,
                IsActiveMembership: false
            );

        return new OperationalCompanyContext(
            companyId,
            userId,
            membership.ProfileId,
            membership.IsActive
        );
    }

    private async Task<Guid> ResolveOperationalCompanyIdAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        if (_currentCompany.HasCompanyContext && _currentCompany.CompanyId != Guid.Empty)
            return _currentCompany.CompanyId;

        var memberships = await _repo.GetActiveCompanyUserMembershipsForUserSystemAsync(
            userId,
            cancellationToken
        );
        var companies = await _companyRepository.GetByIdsAsync(
            memberships.Select(m => m.CompanyId).ToList(),
            cancellationToken
        );
        var inTenant = companies.Where(c => c.TenantId == _currentTenant.TenantId).ToList();
        return inTenant.Count == 1 ? inTenant[0].Id : Guid.Empty;
    }
}

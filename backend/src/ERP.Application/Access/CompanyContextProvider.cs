using ERP.Application.Common;
using ERP.Application.Common.Security;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Interfaces;

namespace ERP.Application.Access;

public sealed class CompanyContextProvider : ICompanyContextProvider
{
    private readonly IAccessRepository _repo;
    private readonly ICompanyRepository _companyRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;
    private readonly IOperatorCompanyAccessPolicy _operatorAccessPolicy;

    public CompanyContextProvider(
        IAccessRepository repo,
        ICompanyRepository companyRepository,
        ICurrentTenant currentTenant,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser,
        IOperatorCompanyAccessPolicy operatorAccessPolicy
    )
    {
        _repo = repo;
        _companyRepository = companyRepository;
        _currentTenant = currentTenant;
        _currentCompany = currentCompany;
        _currentUser = currentUser;
        _operatorAccessPolicy = operatorAccessPolicy;
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

        var hasHeaderCompany =
            _currentCompany.HasCompanyContext && _currentCompany.CompanyId != Guid.Empty;

        // SEC-04: el administrador global en modo operador no tiene membership en la empresa que
        // opera; la política central de operador es la única fuente que lo sustituye.
        if (
            userId == _currentUser.UserId
            && await _operatorAccessPolicy.IsAuthorizedOperatorAsync(cancellationToken)
        )
            return new OperationalCompanyContext(
                hasHeaderCompany ? _currentCompany.CompanyId : Guid.Empty,
                userId,
                null,
                IsActiveMembership: false,
                MembershipRole: SecurityRoles.Admin,
                IsAuthorizedOperator: true
            );

        // SEC-04: un X-Company-Id presente solo vale con membership activa del usuario en esa
        // empresa — nunca se sustituye en silencio por otra empresa del usuario.
        var companyId = hasHeaderCompany
            ? _currentCompany.CompanyId
            : await ResolveSingleMembershipCompanyIdAsync(userId, cancellationToken);
        if (companyId == Guid.Empty)
            return null;

        var membership = await _repo.GetCompanyUserMembershipAsync(
            companyId,
            userId,
            cancellationToken
        );
        if (membership is null || !membership.IsActive)
            return null;

        // La membership no lleva TenantId: la empresa debe pertenecer al tenant del JWT, o un
        // rol/perfil de otro tenant nunca puede evaluarse aquí.
        if (hasHeaderCompany)
        {
            var companies = await _companyRepository.GetByIdsAsync([companyId], cancellationToken);
            if (!companies.Any(c => c.Id == companyId && c.TenantId == _currentTenant.TenantId))
                return null;
        }

        return new OperationalCompanyContext(
            companyId,
            userId,
            membership.ProfileId,
            IsActiveMembership: true,
            MembershipRole: membership.Role
        );
    }

    /// <summary>Sin X-Company-Id: solo la única membership activa del usuario en el tenant.</summary>
    private async Task<Guid> ResolveSingleMembershipCompanyIdAsync(
        Guid userId,
        CancellationToken cancellationToken
    )
    {
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

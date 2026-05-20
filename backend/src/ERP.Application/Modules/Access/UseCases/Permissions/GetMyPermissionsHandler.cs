using ERP.Application.Access.DTOs;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using MediatR;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Access.UseCases.Permissions;

public class GetMyPermissionsHandler : IRequestHandler<GetMyPermissionsQuery, Result<MyPermissionsDto>>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ISubscriberEntitlementsService _entitlements;
    private readonly ISessionModulesResolver _sessionModules;

    public GetMyPermissionsHandler(
        IAccessRepository repo,
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICompanyRepository companyRepository,
        ISubscriberRepository tenantRepository,
        ISubscriberEntitlementsService entitlements,
        ISessionModulesResolver sessionModules)
    {
        _repo = repo;
        _currentUser = currentUser;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _companyRepository = companyRepository;
        _tenantRepository = tenantRepository;
        _entitlements = entitlements;
        _sessionModules = sessionModules;
    }

    public Task<Result<MyPermissionsDto>> HandleAsync(CancellationToken ct = default)
        => Handle(new GetMyPermissionsQuery(), ct);

    public async Task<Result<MyPermissionsDto>> Handle(GetMyPermissionsQuery request, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || !_currentSubscriber.IsAuthenticated)
            return Result<MyPermissionsDto>.Failure("No autenticado.");

        var tenant = await _tenantRepository.GetByIdAsync(_currentSubscriber.SubscriberId, ct);
        var plan = tenant?.PlanCode;
        var modules = await _sessionModules.GetEnabledModuleKeysAsync(_currentSubscriber.SubscriberId, ct);

        var role = _currentUser.Role ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }, plan, modules));

        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(new[] { "*" }, plan, modules));

        var companyId = await ResolveCompanyIdAsync(ct);
        if (companyId == Guid.Empty)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        var membership = await _repo.GetCompanyUserMembershipAsync(companyId, _currentUser.UserId, ct);
        if (membership is null || !membership.IsActive)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        if (membership.ProfileId is null)
            return Result<MyPermissionsDto>.Success(new MyPermissionsDto(Array.Empty<string>(), plan, modules));

        var items = await _repo.GetProfilePermissionsAsync(_currentSubscriber.SubscriberId, membership.ProfileId.Value, ct);
        var candidateKeys = items
            .Where(p => p.IsAllowed)
            .Select(p => p.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var allowed = new List<string>();
        foreach (var key in candidateKeys)
        {
            if (tenant is null ||
                await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
                    _currentSubscriber.SubscriberId, _entitlements, key, ct))
                allowed.Add(key);
        }

        allowed.Sort(StringComparer.OrdinalIgnoreCase);
        return Result<MyPermissionsDto>.Success(new MyPermissionsDto(allowed, plan, modules));
    }

    private async Task<Guid> ResolveCompanyIdAsync(CancellationToken ct)
    {
        if (_currentCompany.HasCompanyContext && _currentCompany.CompanyId != Guid.Empty)
            return _currentCompany.CompanyId;

        var memberships = await _repo.GetActiveCompanyUserMembershipsForUserSystemAsync(_currentUser.UserId, ct);
        var companies = await _companyRepository.GetByIdsAsync(memberships.Select(m => m.CompanyId).ToList(), ct);
        var inSubscriber = companies.Where(c => c.SubscriberId == _currentSubscriber.SubscriberId).ToList();
        return inSubscriber.Count == 1 ? inSubscriber[0].Id : Guid.Empty;
    }
}

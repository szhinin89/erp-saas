using ERP.Application.Common;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Http;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Subscribers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICompanyRepository _companyRepository;
    private readonly ISubscriberRepository _tenantRepository;
    private readonly ISubscriberEntitlementsService _entitlements;

    public PermissionHandler(
        IAccessRepository repo,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICompanyRepository companyRepository,
        ISubscriberRepository tenantRepository,
        ISubscriberEntitlementsService entitlements)
    {
        _repo = repo;
        _currentSubscriber = currentSubscriber;
        _currentCompany = currentCompany;
        _companyRepository = companyRepository;
        _tenantRepository = tenantRepository;
        _entitlements = entitlements;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!_currentSubscriber.IsAuthenticated)
            return;

        var http = context.Resource as HttpContext;
        var ct = http?.RequestAborted ?? CancellationToken.None;

        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var subscriberId = _currentSubscriber.SubscriberId;

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && subscriberId == Guid.Empty)
        {
            context.Succeed(requirement);
            return;
        }

        if (await _tenantRepository.GetByIdAsync(subscriberId, ct) is null)
            return;

        var planAllows = await _entitlements.AllowsPermissionAsync(subscriberId, requirement.PermissionKey, ct);

        // SuperAdmin operating inside a tenant: full access to that tenant, plan-filtered.
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
        {
            if (planAllows)
                context.Succeed(requirement);
            return;
        }

        // Admin: full access to everything the tenant's plan allows.
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (planAllows)
                context.Succeed(requirement);
            return;
        }

        // Regular user: must have the module enabled AND an explicit profile permission.
        if (!planAllows)
            return;

        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
            return;

        var companyId = await ResolveCompanyIdForUserAsync(userId, ct);
        if (companyId == Guid.Empty)
            return;

        var membership = await _repo.GetCompanyUserMembershipAsync(companyId, userId, ct);
        if (membership is null || !membership.IsActive || membership.ProfileId is null)
            return;

        var perm = await _repo.GetProfilePermissionAsync(_currentSubscriber.SubscriberId, membership.ProfileId.Value, requirement.PermissionKey);
        if (perm is not null && perm.IsAllowed)
            context.Succeed(requirement);
    }

    private async Task<Guid> ResolveCompanyIdForUserAsync(Guid userId, CancellationToken ct)
    {
        if (_currentCompany.HasCompanyContext && _currentCompany.CompanyId != Guid.Empty)
            return _currentCompany.CompanyId;

        var memberships = await _repo.GetActiveCompanyUserMembershipsForUserSystemAsync(userId, ct);
        var companies = await _companyRepository.GetByIdsAsync(memberships.Select(m => m.CompanyId).ToList(), ct);
        var inSubscriber = companies.Where(c => c.SubscriberId == _currentSubscriber.SubscriberId).ToList();
        return inSubscriber.Count == 1 ? inSubscriber[0].Id : Guid.Empty;
    }
}

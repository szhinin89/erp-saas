using ERP.Application.Common;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Http;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Tenants.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantEntitlementsService _entitlements;

    public PermissionHandler(
        IAccessRepository repo,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository,
        ITenantEntitlementsService entitlements)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _entitlements = entitlements;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!_currentTenant.IsAuthenticated)
            return;

        var http = context.Resource as HttpContext;
        var ct = http?.RequestAborted ?? CancellationToken.None;

        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var tenantId = _currentTenant.TenantId;

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) && tenantId == Guid.Empty)
        {
            context.Succeed(requirement);
            return;
        }

        if (await _tenantRepository.GetByIdAsync(tenantId, ct) is null)
            return;

        var planAllows = await _entitlements.AllowsPermissionAsync(tenantId, requirement.PermissionKey, ct);

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

        var membership = await _repo.GetMembershipAsync(_currentTenant.TenantId, userId, ct);
        if (membership is null || !membership.IsActive || membership.ProfileId is null)
            return;

        var perm = await _repo.GetProfilePermissionAsync(_currentTenant.TenantId, membership.ProfileId.Value, requirement.PermissionKey);
        if (perm is not null && perm.IsAllowed)
            context.Succeed(requirement);
    }
}

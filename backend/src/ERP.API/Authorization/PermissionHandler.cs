using ERP.Application.Common;
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

    public PermissionHandler(
        IAccessRepository repo,
        ICurrentTenant currentTenant,
        ITenantRepository tenantRepository)
    {
        _repo = repo;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
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

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant is null)
            return;

        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        if (!TenantSubscriptionCatalog.TenantAllowsPermission(tenant, requirement.PermissionKey))
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

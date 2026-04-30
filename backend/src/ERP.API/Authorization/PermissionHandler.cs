using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IAccessRepository _repo;
    private readonly ICurrentTenant _currentTenant;

    public PermissionHandler(IAccessRepository repo, ICurrentTenant currentTenant)
    {
        _repo = repo;
        _currentTenant = currentTenant;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Permisos solo aplican a token de sesión (DefaultPolicy ya exige Session).
        if (!_currentTenant.IsAuthenticated)
            return;

        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? context.User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(sub, out var userId) || userId == Guid.Empty)
            return;

        var membership = await _repo.GetMembershipAsync(_currentTenant.TenantId, userId);
        if (membership is null || !membership.IsActive || membership.ProfileId is null)
            return;

        var perm = await _repo.GetProfilePermissionAsync(_currentTenant.TenantId, membership.ProfileId.Value, requirement.PermissionKey);
        if (perm is not null && perm.IsAllowed)
            context.Succeed(requirement);
    }
}


using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

public sealed class GlobalSuperAdminRequirement : IAuthorizationRequirement;

public sealed class GlobalSuperAdminHandler : AuthorizationHandler<GlobalSuperAdminRequirement>
{
    private readonly ICurrentTenant _currentTenant;

    public GlobalSuperAdminHandler(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GlobalSuperAdminRequirement requirement)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase) &&
            _currentTenant.TenantId == Guid.Empty)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

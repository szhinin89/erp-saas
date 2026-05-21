using ERP.Application.Common;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

public sealed class GlobalSuperAdminRequirement : IAuthorizationRequirement;

public sealed class GlobalSuperAdminHandler : AuthorizationHandler<GlobalSuperAdminRequirement>
{
    private readonly ICurrentSubscriber _currentSubscriber;

    public GlobalSuperAdminHandler(ICurrentSubscriber currentSubscriber)
    {
        _currentSubscriber = currentSubscriber;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GlobalSuperAdminRequirement requirement)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userType = context.User.FindFirst("user_type")?.Value ?? string.Empty;
        var platformRole = context.User.FindFirst("platform_role")?.Value ?? string.Empty;

        var isPlatformSuperAdmin =
            string.Equals(userType, "Platform", StringComparison.OrdinalIgnoreCase)
            && string.Equals(platformRole, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

        var isLegacySuperAdmin =
            string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
            && string.Equals(userType, string.Empty, StringComparison.Ordinal);

        if ((isPlatformSuperAdmin || isLegacySuperAdmin)
            && _currentSubscriber.SubscriberId == Guid.Empty)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

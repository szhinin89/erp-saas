using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

public sealed class GlobalPlatformOperatorRequirement : IAuthorizationRequirement;

public sealed class GlobalPlatformOperatorHandler : AuthorizationHandler<GlobalPlatformOperatorRequirement>
{
    private readonly ICurrentSubscriber _currentSubscriber;

    public GlobalPlatformOperatorHandler(ICurrentSubscriber currentSubscriber)
    {
        _currentSubscriber = currentSubscriber;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GlobalPlatformOperatorRequirement requirement)
    {
        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var userType = context.User.FindFirst("user_type")?.Value ?? string.Empty;
        var platformRole = context.User.FindFirst("platform_role")?.Value ?? string.Empty;

        var isPlatformOperatorUser =
            string.Equals(userType, "Platform", StringComparison.OrdinalIgnoreCase)
            && (string.Equals(platformRole, nameof(PlatformRole.PlatformOperator), StringComparison.OrdinalIgnoreCase)
                || PlatformAuthConstants.IsJwtPlatformOperatorRole(platformRole));

        var isLegacyRoleClaim = PlatformAuthConstants.IsJwtPlatformOperatorRole(role);

        if ((isPlatformOperatorUser || isLegacyRoleClaim)
            && _currentSubscriber.SubscriberId == Guid.Empty)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

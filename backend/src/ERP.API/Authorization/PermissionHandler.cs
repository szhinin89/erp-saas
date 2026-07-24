using ERP.Application.Access.Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ERP.API.Authorization;

/// <summary>
/// Evalúa políticas <c>perm:*</c>. Thin adapter — delega a <see cref="IRuntimePermissionAuthorizer"/>.
/// No contiene lógica de permisos ni acceso a repositorios.
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IRuntimePermissionAuthorizer _authorizer;

    public PermissionHandler(IRuntimePermissionAuthorizer authorizer)
    {
        _authorizer = authorizer;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var http = context.Resource as HttpContext;
        var cancellationToken = http?.RequestAborted ?? CancellationToken.None;

        var role = context.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var sub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        _ = Guid.TryParse(sub, out var userId);

        if (await _authorizer.IsAuthorizedAsync(requirement.PermissionKey, userId, role, cancellationToken))
            context.Succeed(requirement);
    }
}

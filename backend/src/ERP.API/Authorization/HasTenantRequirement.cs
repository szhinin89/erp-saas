using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Authorization;

/// <summary>
/// Exige que el JWT contenga un tenant_id válido (no Empty).
/// Los tokens de bootstrap tienen tenant_id = Guid.Empty y fallan este requisito.
/// </summary>
public sealed class HasTenantRequirement : IAuthorizationRequirement { }

public sealed class HasTenantHandler : AuthorizationHandler<HasTenantRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, HasTenantRequirement requirement)
    {
        var tenantId = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantId, out var id) && id != Guid.Empty)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

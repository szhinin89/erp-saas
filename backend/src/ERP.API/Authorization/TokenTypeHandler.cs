using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Authorization;

public sealed class TokenTypeHandler : AuthorizationHandler<TokenTypeRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TokenTypeRequirement requirement)
    {
        var tokenType = context.User.FindFirst("token_type")?.Value;
        if (string.Equals(tokenType, requirement.TokenType, StringComparison.OrdinalIgnoreCase))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}


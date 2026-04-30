using Microsoft.AspNetCore.Authorization;

namespace ERP.API.Authorization;

public sealed class TokenTypeRequirement : IAuthorizationRequirement
{
    public TokenTypeRequirement(string tokenType)
    {
        TokenType = tokenType;
    }

    public string TokenType { get; }
}


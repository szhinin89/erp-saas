using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ERP.API.Tests.Support;

internal static class TestJwtFactory
{
    public static string CreateSessionJwt(Guid tenantId, Guid userId, string role = "Admin")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, "integration@test.local"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("full_name", "Integration User"),
            new Claim(ClaimTypes.Role, role),
            new Claim("token_type", "session"),
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(IntegrationTestConstants.JwtSecretKey));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             "ZHTechnologies",
            audience:           "ERPUsers",
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

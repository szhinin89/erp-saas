using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ERP.API.Tests.Support;

internal static class TestJwtFactory
{
    public static string CreateSessionJwt(
        Guid tenantId,
        Guid userId,
        Guid? companyId = null,
        string role = "Admin"
    )
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("tenant_id", tenantId.ToString()),
            new(ClaimTypes.Role, role),
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(IntegrationTestConstants.JwtSecretKey)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "ZHTechnologies",
            audience: "ERPUsers",
            claims: claims.ToArray(),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

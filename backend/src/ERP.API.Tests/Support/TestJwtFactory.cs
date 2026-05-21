using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ERP.API.Tests.Support;

internal static class TestJwtFactory
{
    public static string CreateSessionJwt(
        Guid subscriberId,
        Guid userId,
        Guid? companyId = null,
        string role = "Admin")
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, "integration@test.local"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("subscriber_id", subscriberId.ToString()),
            new("full_name", "Integration User"),
            new(ClaimTypes.Role, role),
            new("token_type", "session"),
            new("user_type", "subscriber"),
        };

        if (companyId is Guid cid && cid != Guid.Empty)
            claims.Add(new Claim("company_id", cid.ToString()));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(IntegrationTestConstants.JwtSecretKey));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             "ZHTechnologies",
            audience:           "ERPUsers",
            claims:             claims.ToArray(),
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string CreatePlatformSuperAdminJwt(Guid userId, string email = "superadmin@test.local")
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("subscriber_id", Guid.Empty.ToString()),
            new("full_name", "Platform SuperAdmin"),
            new(ClaimTypes.Role, "SuperAdmin"),
            new("token_type", "session"),
            new("user_type", "Platform"),
            new("platform_role", "SuperAdmin"),
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(IntegrationTestConstants.JwtSecretKey));

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             "ZHTechnologies",
            audience:           "ERPUsers",
            claims:             claims.ToArray(),
            expires:            DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ERP.Infrastructure.Services;

public class AccessTokenService : IAccessTokenService
{
    private readonly IConfiguration _configuration;

    public AccessTokenService(IConfiguration configuration) => _configuration = configuration;

    public string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> tenantIds)
    {
        var expMinutes = int.Parse(_configuration["Jwt:BootstrapExpirationMinutes"] ?? "5", CultureInfo.InvariantCulture);
        var extra = IdentityClaims(user).Append(
            new Claim("tenant_ids", string.Join(',', tenantIds.Select(t => t.ToString()))));
        return GenerateToken(user.Id, Guid.Empty, "Bootstrap", DateTime.UtcNow.AddMinutes(expMinutes), extra);
    }

    public string GenerateSessionToken(IdentityUser user, Guid tenantId, string role)
    {
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);
        return GenerateToken(user.Id, tenantId, role, DateTime.UtcNow.AddMinutes(expMinutes), IdentityClaims(user));
    }

    public string GenerateSessionToken(Guid userId, Guid tenantId, string role)
    {
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60", CultureInfo.InvariantCulture);
        return GenerateToken(userId, tenantId, role, DateTime.UtcNow.AddMinutes(expMinutes), []);
    }

    /// <summary>
    /// Claims de identidad visible (nombre/email) embebidas en el token — snapshot al momento
    /// de emisión. Fuente única para <c>CurrentUserService.FullName</c>/<c>Email</c>, que a su
    /// vez alimenta <c>HttpAuditContext.Actor.UserName</c> (infraestructura de auditoría).
    ///
    /// <c>ClaimTypes.Name</c> — no <c>ClaimTypes.GivenName</c> — representa el nombre visible
    /// completo (<c>IdentityUser.FullName</c> = FirstName + LastName). <c>GivenName</c> es
    /// semánticamente "solo el nombre" y quedó descartado por representar mal el dato real.
    /// </summary>
    private static IEnumerable<Claim> IdentityClaims(IdentityUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.FullName),
            new("username", user.Username),
        };
        if (user.Email is not null)
            claims.Add(new Claim(ClaimTypes.Email, user.Email.Value));
        return claims;
    }

    private string GenerateToken(
        Guid userId, Guid tenantId, string role, DateTime expiresAtUtc,
        IEnumerable<Claim> extraClaims)
    {
        var secretKey = _configuration["Jwt:SecretKey"]!;
        var issuer    = _configuration["Jwt:Issuer"]!;
        var audience  = _configuration["Jwt:Audience"]!;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim(ClaimTypes.Role, role),
        };

        claims.AddRange(extraClaims);

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims,
            expires: expiresAtUtc, signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

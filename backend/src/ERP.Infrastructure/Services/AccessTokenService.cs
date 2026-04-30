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

    public AccessTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> tenantIds)
    {
        return GenerateBootstrapToken(
            userId: user.Id,
            email: user.Email.Value,
            fullName: user.FullName,
            role: "Bootstrap",
            tenantIds: tenantIds);
    }

    public string GenerateSessionToken(IdentityUser user, Guid tenantId, string role)
    {
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        return GenerateSessionToken(
            userId: user.Id,
            email: user.Email.Value,
            fullName: user.FullName,
            tenantId: tenantId,
            role: role);
    }

    public string GenerateBootstrapToken(
        Guid userId,
        string email,
        string fullName,
        string role,
        IReadOnlyList<Guid> tenantIds)
    {
        var expMinutes = int.Parse(_configuration["Jwt:BootstrapExpirationMinutes"] ?? "5");

        var extra = new[]
        {
            new Claim("tenant_ids", string.Join(',', tenantIds.Select(t => t.ToString())))
        };

        return GenerateToken(
            userId: userId,
            email: email,
            fullName: fullName,
            tenantId: Guid.Empty,
            role: role,
            tokenType: "bootstrap",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(expMinutes),
            extraClaims: extra);
    }

    public string GenerateSessionToken(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        string role)
    {
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

        return GenerateToken(
            userId: userId,
            email: email,
            fullName: fullName,
            tenantId: tenantId,
            role: role,
            tokenType: "session",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(expMinutes),
            extraClaims: Array.Empty<Claim>());
    }

    private string GenerateToken(
        Guid userId,
        string email,
        string fullName,
        Guid tenantId,
        string role,
        string tokenType,
        DateTime expiresAtUtc,
        IReadOnlyList<Claim> extraClaims)
    {
        var secretKey = _configuration["Jwt:SecretKey"]!;
        var issuer = _configuration["Jwt:Issuer"]!;
        var audience = _configuration["Jwt:Audience"]!;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("full_name", fullName),
            new Claim(ClaimTypes.Role, role),
            new Claim("token_type", tokenType)
        };

        if (extraClaims.Count > 0)
            claims.AddRange(extraClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}


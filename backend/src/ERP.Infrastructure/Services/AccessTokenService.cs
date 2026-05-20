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

    public string GenerateBootstrapToken(IdentityUser user, IReadOnlyList<Guid> subscriberIds)
    {
        return GenerateBootstrapToken(
            userId: user.Id,
            email: user.Email.Value,
            fullName: user.FullName,
            role: "Bootstrap",
            subscriberIds: subscriberIds);
    }

    public string GenerateSessionToken(IdentityUser user, Guid subscriberId, string role, Guid companyId = default)
    {
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        return GenerateSessionToken(
            userId: user.Id,
            email: user.Email.Value,
            fullName: user.FullName,
            subscriberId: subscriberId,
            role: role,
            companyId: companyId);
    }

    public string GenerateBootstrapToken(
        Guid userId,
        string email,
        string fullName,
        string role,
        IReadOnlyList<Guid> subscriberIds)
    {
        var expMinutes = int.Parse(_configuration["Jwt:BootstrapExpirationMinutes"] ?? "5");

        var extra = new[]
        {
            new Claim("subscriber_ids", string.Join(',', subscriberIds.Select(t => t.ToString())))
        };

        return GenerateToken(
            userId: userId,
            email: email,
            fullName: fullName,
            subscriberId: Guid.Empty,
            role: role,
            tokenType: "bootstrap",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(expMinutes),
            extraClaims: extra);
    }

    public string GenerateSessionToken(
        Guid userId,
        string email,
        string fullName,
        Guid subscriberId,
        string role,
        Guid companyId = default)
    {
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

        var extra = new List<Claim>();
        if (companyId != Guid.Empty)
        {
            extra.Add(new Claim("company_id", companyId.ToString()));
            extra.Add(new Claim("company_role", role));
        }

        return GenerateToken(
            userId: userId,
            email: email,
            fullName: fullName,
            subscriberId: subscriberId,
            role: role,
            tokenType: "session",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(expMinutes),
            extraClaims: extra);
    }

    private string GenerateToken(
        Guid userId,
        string email,
        string fullName,
        Guid subscriberId,
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
            new Claim("subscriber_id", subscriberId.ToString()),
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


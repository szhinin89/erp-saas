using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
        => GenerateToken(user, user.TenantId);

    public string GenerateToken(User user, Guid tenantIdOverride)
    {
        var secretKey  = _configuration["Jwt:SecretKey"]!;
        var issuer     = _configuration["Jwt:Issuer"]!;
        var audience   = _configuration["Jwt:Audience"]!;
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim("tenant_id", tenantIdOverride.ToString()),
            new Claim("full_name", user.FullName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("token_type", "session")
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

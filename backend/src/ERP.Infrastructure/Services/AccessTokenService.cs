using ERP.Application.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
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
        if (user.IsPrimaryPlatformOperator)
        {
            return GenerateBootstrapToken(
                userId: user.Id,
                email: user.Email.Value,
                fullName: user.FullName,
                role: PlatformAuthConstants.JwtPlatformOperatorRole,
                subscriberIds: subscriberIds,
                userType: IdentityUserType.Platform,
                platformRole: PlatformRole.PlatformOperator);
        }

        return GenerateBootstrapToken(
            userId: user.Id,
            email: user.Email.Value,
            fullName: user.FullName,
            role: "Bootstrap",
            subscriberIds: subscriberIds,
            userType: user.UserType,
            platformRole: user.PlatformRole);
    }

    public string GenerateSessionToken(IdentityUser user, Guid subscriberId, string role, Guid companyId = default)
        => GenerateSessionToken(
            userId: user.Id,
            email: user.Email.Value,
            fullName: user.FullName,
            subscriberId: subscriberId,
            role: role,
            companyId: companyId,
            userType: user.UserType,
            platformRole: user.PlatformRole);

    public string GeneratePlatformSessionToken(IdentityUser platformUser)
    {
        if (!platformUser.IsPlatformOperator)
            throw new InvalidOperationException("Solo operadores platform pueden usar tokens platform.");

        var jwtRole = MapPlatformRoleToJwtRole(platformUser.PlatformRole!.Value);
        var expMinutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        return GenerateToken(
            userId: platformUser.Id,
            email: platformUser.Email.Value,
            fullName: platformUser.FullName,
            subscriberId: Guid.Empty,
            role: jwtRole,
            tokenType: "session",
            expiresAtUtc: DateTime.UtcNow.AddMinutes(expMinutes),
            userType: IdentityUserType.Platform,
            platformRole: platformUser.PlatformRole,
            extraClaims: []);
    }

    private static string MapPlatformRoleToJwtRole(PlatformRole role) => role switch
    {
        PlatformRole.PlatformOperator => PlatformAuthConstants.JwtPlatformOperatorRole,
        PlatformRole.Support => "Support",
        PlatformRole.BillingAdmin => "BillingAdmin",
        PlatformRole.Auditor => "Auditor",
        _ => PlatformAuthConstants.JwtPlatformOperatorRole,
    };

    public string GenerateBootstrapToken(
        Guid userId,
        string email,
        string fullName,
        string role,
        IReadOnlyList<Guid> subscriberIds,
        IdentityUserType userType = IdentityUserType.Company,
        PlatformRole? platformRole = null)
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
            userType: userType,
            platformRole: platformRole,
            extraClaims: extra);
    }

    public string GenerateSessionToken(
        Guid userId,
        string email,
        string fullName,
        Guid subscriberId,
        string role,
        Guid companyId = default,
        IdentityUserType userType = IdentityUserType.Company,
        PlatformRole? platformRole = null)
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
            userType: userType,
            platformRole: platformRole,
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
        IdentityUserType userType,
        PlatformRole? platformRole,
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
            new Claim("token_type", tokenType),
            new Claim("user_type", userType.ToString()),
        };

        if (platformRole is not null)
            claims.Add(new Claim("platform_role", platformRole.Value.ToString()));

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

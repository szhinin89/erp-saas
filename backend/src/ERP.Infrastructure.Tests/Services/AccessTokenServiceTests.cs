using ERP.Domain.Access.Entities;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// Verifica que el token de sesión embebe el nombre/email del usuario como claims —
/// la fuente que <c>CurrentUserService.FullName</c>/<c>Email</c> lee para alimentar
/// <c>HttpAuditContext.Actor.UserName</c> (snapshot histórico de auditoría).
/// </summary>
public sealed class AccessTokenServiceTests
{
    private static AccessTokenService BuildService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SecretKey"] = "test-secret-key-at-least-32-characters-long!!",
                    ["Jwt:Issuer"] = "erp-saas-tests",
                    ["Jwt:Audience"] = "erp-saas-tests",
                    ["Jwt:ExpirationMinutes"] = "60",
                    ["Jwt:BootstrapExpirationMinutes"] = "5",
                }
            )
            .Build();

        return new AccessTokenService(config);
    }

    [Fact]
    public void GenerateSessionToken_embeds_email_and_full_name_claims_from_the_identity_user()
    {
        var user = IdentityUser.Create(
            "juan.perez",
            "Juan",
            "Pérez",
            "juan.perez@example.com",
            "hash",
            Guid.NewGuid()
        );
        var service = BuildService();

        var jwt = service.GenerateSessionToken(user, Guid.NewGuid(), "Admin");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token
            .Claims.Should()
            .Contain(c => c.Type == ClaimTypes.Email && c.Value == "juan.perez@example.com");
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "Juan Pérez");
        token.Claims.Should().Contain(c => c.Type == "username" && c.Value == "juan.perez");
        // GivenName representa "solo el nombre", no el nombre completo — no debe usarse para esto.
        token.Claims.Should().NotContain(c => c.Type == ClaimTypes.GivenName);
    }

    [Fact]
    public void GenerateBootstrapToken_also_embeds_identity_claims()
    {
        var user = IdentityUser.Create(
            "ana.gomez",
            "Ana",
            "Gómez",
            "ana.gomez@example.com",
            "hash",
            Guid.NewGuid()
        );
        var service = BuildService();

        var jwt = service.GenerateBootstrapToken(user, [Guid.NewGuid()]);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token
            .Claims.Should()
            .Contain(c => c.Type == ClaimTypes.Email && c.Value == "ana.gomez@example.com");
        token.Claims.Should().Contain(c => c.Type == ClaimTypes.Name && c.Value == "Ana Gómez");
    }

    [Fact]
    public void GenerateSessionToken_by_primitive_userId_does_not_claim_to_know_a_name()
    {
        var service = BuildService();

        var jwt = service.GenerateSessionToken(Guid.NewGuid(), Guid.NewGuid(), "Admin");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);

        token.Claims.Should().NotContain(c => c.Type == ClaimTypes.Email);
        token.Claims.Should().NotContain(c => c.Type == ClaimTypes.Name);
    }
}

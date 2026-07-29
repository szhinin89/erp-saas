using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// Verifica que <see cref="CurrentUserService.FullName"/> lee la claim vigente
/// (<c>ClaimTypes.Name</c>, emitida por <c>AccessTokenService</c>) y mantiene, solo por
/// compatibilidad transitoria, el fallback a <c>ClaimTypes.GivenName</c> para tokens ya
/// emitidos antes de la corrección — nunca al revés.
/// </summary>
public sealed class CurrentUserServiceTests
{
    private static CurrentUserService BuildService(params Claim[] claims)
    {
        var identity = claims.Length == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(claims, "TestAuth");

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new CurrentUserService(accessor);
    }

    [Fact]
    public void FullName_reads_from_the_current_Name_claim()
    {
        var service = BuildService(new Claim(ClaimTypes.Name, "Juan Pérez"));

        service.FullName.Should().Be("Juan Pérez");
    }

    [Fact]
    public void FullName_falls_back_to_legacy_GivenName_claim_for_tokens_issued_before_the_fix()
    {
        // Compat transitoria: tokens emitidos antes de esta corrección solo tienen GivenName.
        var service = BuildService(new Claim(ClaimTypes.GivenName, "Ana Gómez"));

        service.FullName.Should().Be("Ana Gómez");
    }

    [Fact]
    public void FullName_prefers_Name_claim_over_legacy_GivenName_when_both_are_present()
    {
        var service = BuildService(
            new Claim(ClaimTypes.Name, "Nombre Correcto"),
            new Claim(ClaimTypes.GivenName, "Nombre Legado"));

        service.FullName.Should().Be("Nombre Correcto");
    }

    [Fact]
    public void FullName_is_null_when_no_identity_claims_are_present()
    {
        var service = BuildService();

        service.FullName.Should().BeNull();
    }

    [Fact]
    public void Email_reads_from_the_Email_claim()
    {
        var service = BuildService(new Claim(ClaimTypes.Email, "juan.perez@example.com"));

        service.Email.Should().Be("juan.perez@example.com");
    }
}

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Contracts;
using ERP.API.Tests.Support;
using ERP.Application.Auth.DTOs;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Matriz HTTP de refresh token: cookie, body, vacío, expirado, revocado y concurrencia.
/// </summary>
public sealed class RefreshTokenSecurityMatrixTests
{
    [Fact]
    public async Task Refresh_solo_cookie_httpOnly_devuelve_200_y_rota()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (rawToken, _, _) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);

        var response = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { CookieToken = rawToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var setCookie).Should().BeTrue(
            "debe rotar la cookie httpOnly en cada refresh exitoso");
        setCookie!.Any(v => v.Contains(RefreshTokenHttpTestSupport.CookieName)).Should().BeTrue();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(
            RefreshTokenHttpTestSupport.JsonOpts);
        payload!.Success.Should().BeTrue();
        payload.ResponseObject.Token.Should().NotBeNullOrWhiteSpace();
        payload.ResponseObject.RefreshToken.Should().NotBe(rawToken);

        var retryOld = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { CookieToken = rawToken });
        retryOld.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "el token original debe quedar invalidado tras la rotación");
    }

    [Fact]
    public async Task Refresh_solo_body_token_devuelve_200()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (rawToken, _, _) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);

        var response = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = rawToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(
            RefreshTokenHttpTestSupport.JsonOpts);
        payload!.ResponseObject.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_cookie_y_body_simultaneo_prioriza_body()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (cookieToken, user, subscriberId) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);
        var bodyToken = await RefreshTokenHttpTestSupport.CreateSecondRefreshTokenAsync(factory, user.Id, subscriberId);

        var response = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { CookieToken = cookieToken, BodyToken = bodyToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "cuando body y cookie difieren, el body tiene precedencia (clientes móviles/legacy)");

        var retryCookie = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { CookieToken = cookieToken });
        retryCookie.StatusCode.Should().Be(HttpStatusCode.OK,
            "la cookie no usada debe seguir válida si el body tuvo precedencia");

        var retryBody = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = bodyToken });
        retryBody.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "debe haber rotado el token del body, no el de la cookie");
    }

    [Fact]
    public async Task Refresh_body_vacio_sin_cookie_devuelve_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);

        var response = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { SendEmptyJsonBody = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_sin_cookie_ni_body_token_devuelve_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);

        var response = await client.PostAsync("/api/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_token_expirado_devuelve_401_sin_rotar()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (rawToken, _, _) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);
        await RefreshTokenHttpTestSupport.ExpireRefreshTokenInDatabaseAsync(factory, rawToken);

        var response = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = rawToken });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().Should().Contain("expirado");
    }

    [Fact]
    public async Task Refresh_reuso_inmediato_tras_rotacion_no_revoca_familia()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (rawToken, user, subscriberId) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);
        await RefreshTokenHttpTestSupport.CreateSecondRefreshTokenAsync(factory, user.Id, subscriberId);

        var first = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = rawToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var reuse = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = rawToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var activos = await db.RefreshTokens.CountAsync(
            t => t.UserId == user.Id && !t.IsRevoked, CancellationToken.None);
        activos.Should().BeGreaterThan(0,
            "reuso concurrente/inmediato tras rotación no debe invalidar otras sesiones");
    }

    [Fact]
    public async Task Refresh_reuso_tardio_tras_rotacion_revoca_solo_familia()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (rawToken, user, subscriberId) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);
        var secondDeviceToken = await RefreshTokenHttpTestSupport.CreateSecondRefreshTokenAsync(factory, user.Id, subscriberId);

        var first = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = rawToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        await RefreshTokenHttpTestSupport.BackdateRevokedRotationAsync(factory, rawToken, TimeSpan.FromSeconds(30));

        var reuse = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = rawToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var secondStillActive = await db.RefreshTokens.AnyAsync(
            t => t.TokenHash == RefreshTokenService.Hash(secondDeviceToken) && !t.IsRevoked,
            CancellationToken.None);
        secondStillActive.Should().BeTrue("reuso tardío revoca la familia comprometida, no otros dispositivos");

        var firstFamilyActive = await db.RefreshTokens.CountAsync(
            t => t.UserId == user.Id && t.SubscriberId == subscriberId && !t.IsRevoked,
            CancellationToken.None);
        firstFamilyActive.Should().Be(1, "solo debe quedar activo el token del segundo dispositivo");
    }

    [Fact]
    public async Task Refresh_concurrente_20_requests_solo_un_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (rawToken, _, _) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => RefreshTokenHttpTestSupport.PostRefreshAsync(
                RefreshTokenHttpTestSupport.CreateAnonymousClient(factory),
                new RefreshHttpRequest { BodyToken = rawToken }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().Be(19);
    }

    [Fact]
    public async Task Refresh_multidispositivo_mantiene_sesiones_independientes()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (tokenA, user, subscriberId) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);
        var tokenB = await RefreshTokenHttpTestSupport.CreateSecondRefreshTokenAsync(factory, user.Id, subscriberId);

        using var clientA = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        using var clientB = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);

        var refreshA = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            clientA, new RefreshHttpRequest { BodyToken = tokenA });
        var refreshB = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            clientB, new RefreshHttpRequest { BodyToken = tokenB });

        refreshA.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshB.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_concurrente_sobre_mismo_token_permite_como_maximo_un_exito()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);
        var (rawToken, _, _) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => RefreshTokenHttpTestSupport.PostRefreshAsync(
                RefreshTokenHttpTestSupport.CreateAnonymousClient(factory),
                new RefreshHttpRequest { BodyToken = rawToken }))
            .ToArray();

        var responses = await Task.WhenAll(tasks);
        var okCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var unauthorizedCount = responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized);

        okCount.Should().Be(1, "solo una rotación debe tener éxito por refresh token");
        unauthorizedCount.Should().Be(7);
    }

    [Fact]
    public async Task Refresh_race_condition_no_deja_multiples_tokens_activos_en_cadena()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        var (rawToken, user, subscriberId) = await RefreshTokenHttpTestSupport.CreateRefreshTokenAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var barrier = new Barrier(6);
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await using var taskScope = factory.Services.CreateAsyncScope();
            var scopedService = taskScope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
            return await scopedService.ValidateAndRotateAsync(rawToken);
        })));

        results.Count(r => r.IsValid).Should().Be(1);

        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var activos = await db.RefreshTokens.CountAsync(
            t => t.UserId == user.Id && t.SubscriberId == subscriberId && !t.IsRevoked,
            CancellationToken.None);
        activos.Should().BeLessThanOrEqualTo(1,
            "tras carrera debe quedar como máximo el token nuevo, sin cadena paralela activa");
    }

    [Fact]
    public async Task Platform_login_cookie_httpOnly_permite_refresh_tras_recarga()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        const string email = "platform.refresh@test.local";
        const string password = "Test123!";

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var actorId = Guid.NewGuid();
            var user = IdentityUser.CreatePlatformSuperAdmin(
                "Platform", "Refresh", email, hasher.HashPassword(password), actorId);
            await db.IdentityUsers.AddAsync(user);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync(
            "/api/platform/auth/login",
            new { email, password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        login.Headers.TryGetValues("Set-Cookie", out var setCookie).Should().BeTrue();
        setCookie!.Any(c =>
            c.Contains("erp_refresh_token", StringComparison.OrdinalIgnoreCase)
            && c.Contains("path=/api", StringComparison.OrdinalIgnoreCase)
            && !c.Contains("path=/api/auth", StringComparison.OrdinalIgnoreCase))
            .Should().BeTrue("login platform debe persistir cookie con Path=/api");

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { });
        refresh.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await refresh.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(
            RefreshTokenHttpTestSupport.JsonOpts);
        payload!.Success.Should().BeTrue();
        payload.ResponseObject.Role.Should().Be("SuperAdmin");
        payload.ResponseObject.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Refresh_token_invalido_no_emite_access_token_utilizable()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = RefreshTokenHttpTestSupport.CreateAnonymousClient(factory);

        var response = await RefreshTokenHttpTestSupport.PostRefreshAsync(
            client, new RefreshHttpRequest { BodyToken = "no-existe-en-bd" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponseDto>>(
            RefreshTokenHttpTestSupport.JsonOpts);
        payload!.Success.Should().BeFalse();
    }
}

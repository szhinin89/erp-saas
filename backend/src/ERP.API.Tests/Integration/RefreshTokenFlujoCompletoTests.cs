using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Auth.UseCases.RefreshToken;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas del flujo completo de Refresh Tokens.
/// Cubre: login → refresh → logout (dispositivo) → logout (todos).
/// Verifica seguridad: hash en BD, rotación single-use, reutilización detectada.
/// </summary>
public sealed class RefreshTokenFlujoCompletoTests
{
    // ── Flujo completo: crear token → rotar → viejo falla ─────────────────

    [Fact]
    public async Task Flujo_refresh_token_valido_rota_y_emite_nuevo_access_token()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var handler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();

        // Seed: crear usuario Identity + Subscriber + CompanyUserMembership
        var (identityUser, subscriberId) = await SeedIdentityUserAsync(db, factory);

        // Simular lo que hace LoginHandler: emitir refresh token
        var (rawToken1, expiry1) = await service.CreateAsync(
            identityUser.Id, subscriberId, null, RefreshUserType.Identity);

        rawToken1.Should().NotBeNullOrEmpty();
        expiry1.Should().BeAfter(DateTime.UtcNow.AddDays(25));

        // Rotar: obtener nuevo access token + nuevo refresh token
        var result = await handler.HandleAsync(rawToken1);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Token.Should().NotBeNullOrEmpty("debe emitir un nuevo access token");
        result.Value.RefreshToken.Should().NotBeNullOrEmpty("debe emitir un nuevo refresh token");
        result.Value.RefreshToken.Should().NotBe(rawToken1, "el refresh token debe rotarse");
        result.Value.RefreshTokenExpiry.Should().BeAfter(DateTime.UtcNow.AddDays(25));
        result.Value.UserId.Should().Be(identityUser.Id);
        result.Value.SubscriberId.Should().Be(subscriberId);
    }

    [Fact]
    public async Task Refresh_token_antiguo_no_funciona_tras_rotacion()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var handler = scope.ServiceProvider.GetRequiredService<RefreshTokenHandler>();

        var (identityUser, subscriberId) = await SeedIdentityUserAsync(db, factory);
        var (rawToken1, _) = await service.CreateAsync(identityUser.Id, subscriberId, null, RefreshUserType.Identity);

        // Primera rotación → éxito
        var result1 = await handler.HandleAsync(rawToken1);
        result1.IsSuccess.Should().BeTrue(result1.Error);

        // Reusar el token original (ya revocado) → falla
        var result2 = await handler.HandleAsync(rawToken1);
        result2.IsSuccess.Should().BeFalse("el token fue rotado — no puede usarse de nuevo");
    }

    // ── Seguridad: hash en BD ─────────────────────────────────────────────

    [Fact]
    public async Task Tokens_se_almacenan_como_SHA256_hash_en_BD()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        var (rawToken, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == userId, CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.TokenHash.Should().NotBe(rawToken,
            "el token plano NO debe guardarse en BD — solo el hash SHA-256");
        stored.TokenHash.Should().Be(RefreshTokenService.Hash(rawToken),
            "debe coincidir con SHA-256 del token plano");
    }

    // ── Detección de reutilización maliciosa ─────────────────────────────

    [Fact]
    public async Task Reutilizar_token_revocado_revoca_todos_y_falla()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        // Crear 2 tokens para el mismo usuario (simula 2 dispositivos)
        var (rawToken1, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);
        await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy); // token de segundo dispositivo

        // Primer uso normal del token1 → rota
        await service.ValidateAndRotateAsync(rawToken1);

        // Intento de reuso del token1 (ya revocado) → detecta posible compromiso
        var resultMalicioso = await service.ValidateAndRotateAsync(rawToken1);

        resultMalicioso.IsValid.Should().BeFalse("token ya fue revocado");
        resultMalicioso.Error.Should().Contain("revocado");

        // Todos los tokens del usuario deben quedar revocados (respuesta a posible compromiso)
        var tokenesActivos = await db.RefreshTokens
            .CountAsync(t => t.UserId == userId && !t.IsRevoked, CancellationToken.None);
        tokenesActivos.Should().Be(0, "la detección de reutilización revoca todos los tokens");
    }

    // ── Logout ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_dispositivo_revoca_solo_el_token_actual()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var handler = scope.ServiceProvider.GetRequiredService<LogoutHandler>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        var (token1, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);
        var (token2, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        var result = await handler.HandleAsync(token1, allDevices: false);

        result.IsSuccess.Should().BeTrue(result.Error);

        var activos = await db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .CountAsync(CancellationToken.None);
        activos.Should().Be(1, "solo el token2 debe quedar activo");

        // El token2 sigue siendo válido
        var validation = await service.ValidateAndRotateAsync(token2);
        validation.IsValid.Should().BeTrue("token2 no fue afectado por el logout del token1");
    }

    [Fact]
    public async Task Logout_global_revoca_todos_los_tokens_del_usuario()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var handler = scope.ServiceProvider.GetRequiredService<LogoutHandler>();

        var (identityUser, subscriberId) = await SeedIdentityUserAsync(db, factory);

        var (token1, _) = await service.CreateAsync(identityUser.Id, subscriberId, null, RefreshUserType.Identity);
        await service.CreateAsync(identityUser.Id, subscriberId, null, RefreshUserType.Identity); // token2

        var result = await handler.HandleAsync(token1, allDevices: true);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Contain("todos los dispositivos");

        var activos = await db.RefreshTokens
            .CountAsync(t => t.UserId == identityUser.Id && !t.IsRevoked, CancellationToken.None);
        activos.Should().Be(0, "todos los tokens deben quedar revocados tras logout global");
    }

    // ── HTTP: endpoint /api/auth/refresh con token inválido ───────────────

    [Fact]
    public async Task POST_refresh_con_token_invalido_devuelve_401()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { refreshToken = "token-que-no-existe-en-BD" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_logout_sin_token_devuelve_400()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new { refreshToken = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Seed helper ───────────────────────────────────────────────────────

    private static async Task<(IdentityUser User, Guid SubscriberId)> SeedIdentityUserAsync(
        ErpDbContext db, IntegrationTestWebAppFactory factory)
    {
        var seed = await IntegrationSeedData.SeedAsync(
            db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);

        var subscriberId = seed.SubscriberId;
        var userId   = seed.UserId;

        // Crear IdentityUser + CompanyUserMembership para el path "Identity"
        var identityUser = IdentityUser.Create("Test", "Refresh", "test.refresh@erp.dev",
            "hashed-password", userId);
        await db.IdentityUsers.AddAsync(identityUser, CancellationToken.None);

        var defaultCompany = await db.Companies.FirstOrDefaultAsync(c => c.SubscriberId == subscriberId);
        if (defaultCompany is null)
        {
            defaultCompany = Company.CreateFromSubscriber(subscriberId, "1799999999001", "Test Company", "Seed Address");
            await db.Companies.AddAsync(defaultCompany, CancellationToken.None);
        }

        var membership = CompanyUserMembership.Create(defaultCompany.Id, identityUser.Id, "Admin", null, userId);
        await db.CompanyUserMemberships.AddAsync(membership, CancellationToken.None);

        await db.SaveChangesAsync(CancellationToken.None);

        factory.MutableUser.UserId = identityUser.Id;
        return (identityUser, subscriberId);
    }
}

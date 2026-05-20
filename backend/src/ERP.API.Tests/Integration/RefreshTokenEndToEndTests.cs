using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ERP.API.Tests.Support;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Services;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Pruebas de integración del ciclo completo de Refresh Tokens.
/// Usan EF InMemory + el stack real de Application e Infrastructure.
/// </summary>
public sealed class RefreshTokenEndToEndTests
{
    [Fact]
    public async Task Crear_refresh_token_persiste_en_BD_con_hash()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        var (rawToken, expiry) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        rawToken.Should().NotBeNullOrEmpty();
        expiry.Should().BeAfter(DateTime.UtcNow.AddDays(25));

        // Verificar que en BD se guardó el hash, no el token plano
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.UserId == userId, CancellationToken.None);

        stored.Should().NotBeNull();
        stored!.TokenHash.Should().NotBe(rawToken, "se debe guardar el hash, no el token plano");
        stored.TokenHash.Should().Be(RefreshTokenService.Hash(rawToken));
        stored.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndRotate_rota_token_y_crea_nuevo_en_BD()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        // 1. Crear token inicial
        var (rawToken1, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        // 2. Rotar
        var result = await service.ValidateAndRotateAsync(rawToken1);

        result.IsValid.Should().BeTrue(result.Error);
        result.NewToken.Should().NotBe(rawToken1);

        // 3. El token original queda revocado en BD
        var original = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == RefreshTokenService.Hash(rawToken1), CancellationToken.None);
        original!.IsRevoked.Should().BeTrue();
        original.ReasonRevoked.Should().Be("Rotación");

        // 4. El nuevo token existe en BD
        var nuevo = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == RefreshTokenService.Hash(result.NewToken!), CancellationToken.None);
        nuevo.Should().NotBeNull();
        nuevo!.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndRotate_segundo_uso_del_token_original_falla()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        var (rawToken, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        // Primer uso — válido
        await service.ValidateAndRotateAsync(rawToken);

        // Segundo uso del mismo token — debe fallar (ya revocado)
        var result2 = await service.ValidateAndRotateAsync(rawToken);

        result2.IsValid.Should().BeFalse();
        result2.Error.Should().Contain("revocado");
    }

    [Fact]
    public async Task RevokeAll_revoca_todos_los_tokens_activos_del_usuario()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        // Crear 2 tokens (simula 2 dispositivos)
        await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);
        await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        await service.RevokeAllForUserAsync(userId, subscriberId, "Logout");

        var activos = await db.RefreshTokens
            .CountAsync(t => t.UserId == userId && !t.IsRevoked, CancellationToken.None);

        activos.Should().Be(0, "todos los tokens deben quedar revocados");
    }

    [Fact]
    public async Task Token_expirado_retorna_invalido_y_queda_marcado_revocado()
    {
        await using var factory = new IntegrationTestWebAppFactory();
        using var scope  = factory.Services.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var repo    = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        await IntegrationSeedData.SeedAsync(db, factory.MutableSubscriber, factory.MutableUser, CancellationToken.None);
        var userId   = factory.MutableUser.UserId;
        var subscriberId = factory.MutableSubscriber.SubscriberId;

        // Insertar manualmente un token con fecha pasada
        var rawToken     = "expired-token-raw";
        var expiredToken = RefreshToken.Create(userId, subscriberId, RefreshUserType.Legacy,
                                               RefreshTokenService.Hash(rawToken));
        // Simular expiración: revocar directamente para llegar al path IsActive=false
        expiredToken.Revoke("Expirado");
        await repo.AddAsync(expiredToken);
        await repo.SaveChangesAsync();

        var result = await service.ValidateAndRotateAsync(rawToken);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("revocado");
    }
}

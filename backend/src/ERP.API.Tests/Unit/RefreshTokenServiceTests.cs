using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Infrastructure.Services;

namespace ERP.API.Tests.Unit;

/// <summary>
/// Tests unitarios de RefreshTokenService con fake in-memory del repositorio.
/// </summary>
public sealed class RefreshTokenServiceTests
{
    // ── Creación ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_persiste_hash_SHA256_no_token_plano()
    {
        var repo    = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId  = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken, expiry) = await service.CreateAsync(userId, tenantId, RefreshUserType.Legacy);

        rawToken.Should().NotBeNullOrEmpty();
        expiry.Should().BeAfter(DateTime.UtcNow.AddDays(25));

        repo.Stored.Should().HaveCount(1);
        repo.Stored[0].TokenHash.Should().NotBe(rawToken, "se persiste el hash, no el token plano");
        repo.Stored[0].TokenHash.Should().Be(RefreshTokenService.Hash(rawToken));
        repo.Stored[0].UserType.Should().Be(RefreshUserType.Legacy);
    }

    // ── Validación y rotación ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateAndRotate_token_valido_rota_y_devuelve_datos_correctos()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken1, _) = await service.CreateAsync(userId, tenantId, RefreshUserType.Legacy);
        var result = await service.ValidateAndRotateAsync(rawToken1);

        result.IsValid.Should().BeTrue(result.Error);
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.UserType.Should().Be(RefreshUserType.Legacy);
        result.NewToken.Should().NotBeNullOrEmpty().And.NotBe(rawToken1);

        var original = repo.Stored.First(t => t.TokenHash == RefreshTokenService.Hash(rawToken1));
        original.IsRevoked.Should().BeTrue();
        original.ReasonRevoked.Should().Be("Rotación");
    }

    [Fact]
    public async Task ValidateAndRotate_token_inexistente_falla()
    {
        var service = Build(new FakeRefreshTokenRepository());
        var result  = await service.ValidateAndRotateAsync("token-que-no-existe");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("no válido");
    }

    [Fact]
    public async Task ValidateAndRotate_token_ya_revocado_falla_y_revoca_todos()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        // Crear 2 tokens activos + 1 revocado que es el que intentamos reusar
        var revocado = RefreshToken.Create(userId, tenantId, RefreshUserType.Legacy, "hash-revocado");
        revocado.Revoke("Test");
        repo.Stored.Add(revocado);

        var activo = RefreshToken.Create(userId, tenantId, RefreshUserType.Legacy, "hash-activo");
        repo.Stored.Add(activo);

        repo.SetupHash("hash-revocado-raw", revocado);

        var result = await service.ValidateAndRotateAsync("hash-revocado-raw");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("revocado");

        // Todos los tokens activos del usuario también deben quedar revocados
        activo.IsRevoked.Should().BeTrue("reutilización maliciosa → revocar todo");
    }

    // ── Revocación masiva ─────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAll_revoca_todos_los_tokens_activos()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await service.CreateAsync(userId, tenantId, RefreshUserType.Legacy);
        await service.CreateAsync(userId, tenantId, RefreshUserType.Legacy);

        await service.RevokeAllForUserAsync(userId, tenantId, "Logout");

        repo.Stored.All(t => t.IsRevoked).Should().BeTrue();
        repo.Stored.All(t => t.ReasonRevoked == "Logout").Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAll_sin_tokens_no_falla()
    {
        var service = Build(new FakeRefreshTokenRepository());
        var act     = async () => await service.RevokeAllForUserAsync(Guid.NewGuid(), Guid.NewGuid(), "Logout");
        await act.Should().NotThrowAsync();
    }

    // ── Hash ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_mismo_input_produce_mismo_output()
    {
        var h1 = RefreshTokenService.Hash("test-token");
        var h2 = RefreshTokenService.Hash("test-token");
        h1.Should().Be(h2);
    }

    [Fact]
    public void Hash_distinto_input_produce_distinto_output()
    {
        var h1 = RefreshTokenService.Hash("token-A");
        var h2 = RefreshTokenService.Hash("token-B");
        h1.Should().NotBe(h2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static RefreshTokenService Build(FakeRefreshTokenRepository repo)
        => new(repo, NullLogger<RefreshTokenService>.Instance);

    // ── Fake repository ───────────────────────────────────────────────────

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Stored { get; } = new();

        private readonly Dictionary<string, RefreshToken> _byHash = new();

        public void SetupHash(string rawToken, RefreshToken entity)
            => _byHash[RefreshTokenService.Hash(rawToken)] = entity;

        public Task AddAsync(RefreshToken token, CancellationToken ct = default)
        {
            Stored.Add(token);
            _byHash[token.TokenHash] = token;
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        {
            _byHash.TryGetValue(tokenHash, out var token);
            return Task.FromResult(token);
        }

        public Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
            Guid userId, Guid tenantId, CancellationToken ct = default)
        {
            var now    = DateTime.UtcNow;
            var result = (IReadOnlyList<RefreshToken>)Stored
                .Where(t => t.UserId == userId && t.TenantId == tenantId && !t.IsRevoked && t.ExpiresAt > now)
                .ToList();
            return Task.FromResult(result);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}

using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ERP.Application.Common.Config;
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
    public async Task Create_persiste_hash_SHA256_y_familyId()
    {
        var repo    = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId  = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        var (rawToken, expiry) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        rawToken.Should().NotBeNullOrEmpty();
        expiry.Should().BeAfter(DateTime.UtcNow.AddDays(25));

        repo.Stored.Should().HaveCount(1);
        repo.Stored[0].TokenHash.Should().Be(RefreshTokenService.Hash(rawToken));
        repo.Stored[0].FamilyId.Should().Be(repo.Stored[0].Id);
        repo.Stored[0].RotationDepth.Should().Be(0);
    }

    // ── Validación y rotación ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateAndRotate_token_valido_rota_y_hereda_familia()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        var (rawToken1, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);
        var original = repo.Stored[0];
        var result = await service.ValidateAndRotateAsync(rawToken1);

        result.IsValid.Should().BeTrue(result.Error);
        result.NewToken.Should().NotBeNullOrEmpty().And.NotBe(rawToken1);

        var successor = repo.Stored.First(t => t.TokenHash == RefreshTokenService.Hash(result.NewToken!));
        successor.FamilyId.Should().Be(original.FamilyId);
        successor.ParentTokenId.Should().Be(original.Id);
        successor.RotationDepth.Should().Be(1);

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
    public async Task ValidateAndRotate_reuso_sospechoso_revoca_solo_familia()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        var revocado = RefreshToken.Create(userId, subscriberId, null, RefreshUserType.Legacy, "hash-revocado");
        revocado.Revoke("Test");
        repo.Stored.Add(revocado);
        repo.SetupHash("hash-revocado-raw", revocado);

        var activoOtraFamilia = RefreshToken.Create(userId, subscriberId, null, RefreshUserType.Legacy, "hash-activo");
        repo.Stored.Add(activoOtraFamilia);

        var result = await service.ValidateAndRotateAsync("hash-revocado-raw");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("revocado");
        activoOtraFamilia.IsRevoked.Should().BeFalse("otra sesión/dispositivo no debe invalidarse");
    }

    [Fact]
    public async Task ValidateAndRotate_reuso_benigno_no_revoca_familia()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        var (raw, _) = await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);
        await service.ValidateAndRotateAsync(raw);

        var revoked = repo.Stored.First(t => t.TokenHash == RefreshTokenService.Hash(raw));
        var reuse = await service.ValidateAndRotateAsync(raw);

        reuse.IsValid.Should().BeFalse();
        repo.Stored.Count(t => !t.IsRevoked).Should().BeGreaterThan(0);
    }

    // ── Revocación masiva ─────────────────────────────────────────────────

    [Fact]
    public async Task RevokeAll_revoca_todos_los_tokens_activos()
    {
        var repo     = new FakeRefreshTokenRepository();
        var service  = Build(repo);
        var userId   = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);
        await service.CreateAsync(userId, subscriberId, null, RefreshUserType.Legacy);

        await service.RevokeAllForUserAsync(userId, subscriberId, "Logout");

        repo.Stored.All(t => t.IsRevoked).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeFamily_revoca_solo_tokens_de_la_familia()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var subscriberId = Guid.NewGuid();

        var a = RefreshToken.Create(userId, subscriberId, null, RefreshUserType.Legacy, "h1");
        var b = RefreshToken.Create(userId, subscriberId, null, RefreshUserType.Legacy, "h2");
        repo.Stored.AddRange([a, b]);

        await service.RevokeFamilyAsync(a.FamilyId, "Compromiso");

        a.IsRevoked.Should().BeTrue();
        b.IsRevoked.Should().BeFalse();
    }

    // ── Hash ──────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_mismo_input_produce_mismo_output()
    {
        RefreshTokenService.Hash("test-token").Should().Be(RefreshTokenService.Hash("test-token"));
    }

    private static RefreshTokenService Build(FakeRefreshTokenRepository repo)
    {
        var cache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var rateLimiter = new RefreshTokenRateLimiter(cache, NullLogger<RefreshTokenRateLimiter>.Instance);
        var options = Options.Create(new AuthOptions { RefreshRotationGraceSeconds = 5 });
        return new RefreshTokenService(repo, rateLimiter, options, NullLogger<RefreshTokenService>.Instance);
    }

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
            Guid userId, Guid subscriberId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            return Task.FromResult<IReadOnlyList<RefreshToken>>(Stored
                .Where(t => t.UserId == userId && t.SubscriberId == subscriberId && !t.IsRevoked && t.ExpiresAt > now)
                .ToList());
        }

        public async Task<(bool Success, RefreshToken? Previous)> TryRotateAsync(
            string tokenHash, RefreshToken successor, CancellationToken ct = default)
        {
            if (!_byHash.TryGetValue(tokenHash, out var token) || !token.IsActive)
                return (false, token);

            token.Revoke("Rotación", successor.TokenHash);
            await AddAsync(successor, ct);
            return (true, token);
        }

        public Task<int> RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var count = 0;
            foreach (var t in Stored.Where(t => t.FamilyId == familyId && !t.IsRevoked && t.ExpiresAt > now))
            {
                t.Revoke(reason);
                count++;
            }
            return Task.FromResult(count);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}

using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

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
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken, expiry) = await service.CreateAsync(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy
        );

        rawToken.Should().NotBeNullOrEmpty();
        // Con la política por defecto (SessionAbsoluteLifetimeMinutes=480, 8h) el vencimiento
        // individual queda dominado por el límite absoluto de sesión, no por los 30 días de
        // higiene de RefreshTokenIndividualLifetimeMinutes.
        expiry.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(480), TimeSpan.FromMinutes(1));

        repo.Stored.Should().HaveCount(1);
        repo.Stored[0].TokenHash.Should().Be(RefreshTokenService.Hash(rawToken));
        repo.Stored[0].FamilyId.Should().Be(repo.Stored[0].Id);
        repo.Stored[0].RotationDepth.Should().Be(0);
        repo.Stored[0]
            .AbsoluteExpiresAt.Should()
            .BeCloseTo(DateTime.UtcNow.AddMinutes(480), TimeSpan.FromMinutes(1));
        repo.Stored[0].ExpiresAt.Should().Be(repo.Stored[0].AbsoluteExpiresAt);
    }

    // ── Validación y rotación ─────────────────────────────────────────────

    [Fact]
    public async Task ValidateAndRotate_token_valido_rota_y_hereda_familia()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken1, _) = await service.CreateAsync(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy
        );
        var original = repo.Stored[0];
        var result = await service.ValidateAndRotateAsync(rawToken1);

        result.IsValid.Should().BeTrue(result.Error);
        result.NewToken.Should().NotBeNullOrEmpty().And.NotBe(rawToken1);

        var successor = repo.Stored.First(t =>
            t.TokenHash == RefreshTokenService.Hash(result.NewToken!)
        );
        successor.FamilyId.Should().Be(original.FamilyId);
        successor.ParentTokenId.Should().Be(original.Id);
        successor.RotationDepth.Should().Be(1);

        original.IsRevoked.Should().BeTrue();
        original.ReasonRevoked.Should().Be("Rotación");
    }

    // ── Expiración absoluta de sesión (Fase 2) ───────────────────────────────

    [Fact]
    public async Task ValidateAndRotate_dentro_de_la_ventana_absoluta_emite_nuevo_access_y_refresh()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo, new AuthOptions { SessionAbsoluteLifetimeMinutes = 480 });
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken, _) = await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);
        var result = await service.ValidateAndRotateAsync(rawToken);

        result.IsValid.Should().BeTrue(result.Error);
        result.NewToken.Should().NotBeNullOrEmpty();
        result.NewExpiry.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAndRotate_despues_de_vencer_la_ventana_absoluta_falla()
    {
        var repo = new FakeRefreshTokenRepository();
        // Ventana absoluta ya vencida en el pasado: simula una sesión que superó su límite.
        var service = Build(repo, new AuthOptions { SessionAbsoluteLifetimeMinutes = -1 });
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken, _) = await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);
        var result = await service.ValidateAndRotateAsync(rawToken);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Sesión expirada");
        result.IsRateLimited.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAndRotate_la_rotacion_no_extiende_la_expiracion_absoluta()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo, new AuthOptions { SessionAbsoluteLifetimeMinutes = 480 });
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (rawToken1, _) = await service.CreateAsync(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy
        );
        var original = repo.Stored[0];
        var originalAbsolute = original.AbsoluteExpiresAt;

        var result = await service.ValidateAndRotateAsync(rawToken1);
        var successor = repo.Stored.First(t =>
            t.TokenHash == RefreshTokenService.Hash(result.NewToken!)
        );

        successor.AbsoluteExpiresAt.Should().Be(originalAbsolute);
    }

    [Fact]
    public async Task ValidateAndRotate_el_sucesor_hereda_la_misma_expiracion_absoluta_tras_varias_rotaciones()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo, new AuthOptions { SessionAbsoluteLifetimeMinutes = 480 });
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (raw, _) = await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);
        var initialAbsolute = repo.Stored[0].AbsoluteExpiresAt;

        var r1 = await service.ValidateAndRotateAsync(raw);
        var r2 = await service.ValidateAndRotateAsync(r1.NewToken!);
        var r3 = await service.ValidateAndRotateAsync(r2.NewToken!);

        r3.IsValid.Should().BeTrue(r3.Error);
        var thirdSuccessor = repo.Stored.First(t =>
            t.TokenHash == RefreshTokenService.Hash(r3.NewToken!)
        );
        thirdSuccessor.AbsoluteExpiresAt.Should().Be(initialAbsolute);
        thirdSuccessor.RotationDepth.Should().Be(3);
    }

    [Fact]
    public async Task Create_el_vencimiento_individual_nunca_supera_el_limite_absoluto()
    {
        // Ventana absoluta más corta que la vida individual configurada (30 días por defecto):
        // ExpiresAt debe quedar acotado al límite absoluto, nunca extenderlo.
        var repo = new FakeRefreshTokenRepository();
        var service = Build(
            repo,
            new AuthOptions
            {
                RefreshTokenIndividualLifetimeMinutes = 43_200,
                SessionAbsoluteLifetimeMinutes = 60,
            }
        );
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);

        var stored = repo.Stored[0];
        stored.ExpiresAt.Should().Be(stored.AbsoluteExpiresAt);
        stored.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromMinutes(1));
    }

    // ── ValidateWithoutRotatingAsync (Fase 4 — Reautenticación) ──────────────

    [Fact]
    public async Task ValidateWithoutRotating_token_valido_devuelve_identidad_sin_rotar()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        var (rawToken, _) = await service.CreateAsync(
            userId,
            tenantId,
            companyId,
            RefreshUserType.Identity
        );

        var result = await service.ValidateWithoutRotatingAsync(rawToken);

        result.IsValid.Should().BeTrue(result.Error);
        result.UserId.Should().Be(userId);
        result.TenantId.Should().Be(tenantId);
        result.CompanyId.Should().Be(companyId);
        result.NewToken.Should().BeNull();
        result.NewExpiry.Should().BeNull();

        // No rota: el mismo rawToken sigue siendo válido para un refresh normal después.
        repo.Stored.Should().HaveCount(1);
        repo.Stored[0].IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateWithoutRotating_token_revocado_falla()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var revocado = RefreshToken.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            RefreshUserType.Identity,
            "hash-revocado",
            DateTime.UtcNow.AddMinutes(480),
            DateTime.UtcNow.AddMinutes(480)
        );
        revocado.Revoke("Logout");
        repo.Stored.Add(revocado);
        repo.SetupHash("raw-revocado", revocado);

        var result = await service.ValidateWithoutRotatingAsync("raw-revocado");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("revocado");
    }

    [Fact]
    public async Task ValidateWithoutRotating_sesion_absoluta_vencida_falla()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo, new AuthOptions { SessionAbsoluteLifetimeMinutes = -1 });
        var (rawToken, _) = await service.CreateAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            RefreshUserType.Identity
        );

        var result = await service.ValidateWithoutRotatingAsync(rawToken);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Sesión expirada");
    }

    [Fact]
    public async Task ValidateAndRotate_token_inexistente_falla()
    {
        var service = Build(new FakeRefreshTokenRepository());
        var result = await service.ValidateAndRotateAsync("token-que-no-existe");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("no válido");
    }

    [Fact]
    public async Task ValidateAndRotate_reuso_sospechoso_revoca_solo_familia()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var revocado = RefreshToken.Create(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy,
            "hash-revocado",
            DateTime.UtcNow.AddMinutes(480),
            DateTime.UtcNow.AddMinutes(480)
        );
        revocado.Revoke("Test");
        repo.Stored.Add(revocado);
        repo.SetupHash("hash-revocado-raw", revocado);

        var activoOtraFamilia = RefreshToken.Create(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy,
            "hash-activo",
            DateTime.UtcNow.AddMinutes(480),
            DateTime.UtcNow.AddMinutes(480)
        );
        repo.Stored.Add(activoOtraFamilia);

        var result = await service.ValidateAndRotateAsync("hash-revocado-raw");

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("revocado");
        activoOtraFamilia.IsRevoked.Should().BeFalse("otra sesión/dispositivo no debe invalidarse");
    }

    [Fact]
    public async Task ValidateAndRotate_reuso_benigno_no_revoca_familia()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var (raw, _) = await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);
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
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);
        await service.CreateAsync(userId, tenantId, null, RefreshUserType.Legacy);

        await service.RevokeAllForUserAsync(userId, tenantId, "Logout");

        repo.Stored.All(t => t.IsRevoked).Should().BeTrue();
    }

    [Fact]
    public async Task RevokeFamily_revoca_solo_tokens_de_la_familia()
    {
        var repo = new FakeRefreshTokenRepository();
        var service = Build(repo);
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var a = RefreshToken.Create(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy,
            "h1",
            DateTime.UtcNow.AddMinutes(480),
            DateTime.UtcNow.AddMinutes(480)
        );
        var b = RefreshToken.Create(
            userId,
            tenantId,
            null,
            RefreshUserType.Legacy,
            "h2",
            DateTime.UtcNow.AddMinutes(480),
            DateTime.UtcNow.AddMinutes(480)
        );
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

    private static RefreshTokenService Build(
        FakeRefreshTokenRepository repo,
        AuthOptions? authOptions = null
    )
    {
        var cache = new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())
        );
        var rateLimiter = new RefreshTokenRateLimiter(
            cache,
            NullLogger<RefreshTokenRateLimiter>.Instance
        );
        authOptions ??= new AuthOptions { RefreshRotationGraceSeconds = 5 };
        var options = Options.Create(authOptions);
        return new RefreshTokenService(
            repo,
            rateLimiter,
            options,
            new NoOpSecurityMetrics(),
            NullLogger<RefreshTokenService>.Instance
        );
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<RefreshToken> Stored { get; } = new();
        private readonly Dictionary<string, RefreshToken> _byHash = new();

        public void SetupHash(string rawToken, RefreshToken entity) =>
            _byHash[RefreshTokenService.Hash(rawToken)] = entity;

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
            Guid userId,
            Guid tenantId,
            CancellationToken ct = default
        )
        {
            var now = DateTime.UtcNow;
            return Task.FromResult<IReadOnlyList<RefreshToken>>(
                Stored
                    .Where(t =>
                        t.UserId == userId
                        && t.TenantId == tenantId
                        && !t.IsRevoked
                        && t.ExpiresAt > now
                    )
                    .ToList()
            );
        }

        public async Task<(bool Success, RefreshToken? Previous)> TryRotateAsync(
            string tokenHash,
            RefreshToken successor,
            CancellationToken ct = default
        )
        {
            if (!_byHash.TryGetValue(tokenHash, out var token) || !token.IsActive)
                return (false, token);

            token.Revoke("Rotación", successor.TokenHash);
            await AddAsync(successor, ct);
            return (true, token);
        }

        public Task<int> RevokeFamilyAsync(
            Guid familyId,
            string reason,
            CancellationToken ct = default
        )
        {
            var now = DateTime.UtcNow;
            var count = 0;
            foreach (
                var t in Stored.Where(t =>
                    t.FamilyId == familyId && !t.IsRevoked && t.ExpiresAt > now
                )
            )
            {
                t.Revoke(reason);
                count++;
            }
            return Task.FromResult(count);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class NoOpSecurityMetrics : ISecurityMetrics
    {
        public void RecordCrossCompanyDenied(SecurityMetricTags? tags = null) { }

        public void RecordMembershipValidationFailed(SecurityMetricTags? tags = null) { }

        public void RecordInvalidCompanyContext(SecurityMetricTags? tags = null) { }

        public void RecordJwtRefreshRevoked(SecurityMetricTags? tags = null) { }

        public void RecordPermissionDenied(SecurityMetricTags? tags = null) { }

        public void RecordMasterDataDualWriteFailed(SecurityMetricTags? tags = null) { }

        public void RecordMasterDataSyncInconsistency(SecurityMetricTags? tags = null) { }

        public void RecordBackgroundContextLeakDetected(SecurityMetricTags? tags = null) { }

        public void RecordNamespaceFallbackUsed(SecurityMetricTags? tags = null) { }
    }
}

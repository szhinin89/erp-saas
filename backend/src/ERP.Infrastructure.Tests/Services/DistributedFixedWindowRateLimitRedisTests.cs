using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 (verificación final) — con Redis real el incremento es
/// atómico en el servidor: 20 solicitudes concurrentes, incluso desde dos "nodos" con conexiones
/// distintas, nunca superan el límite. Requiere Docker.
/// </summary>
[Trait("Category", "Redis")]
public sealed class DistributedFixedWindowRateLimitRedisTests : IAsyncLifetime
{
    private const int Limit = 3;

    private readonly IContainer _redis = new ContainerBuilder()
        .WithImage("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(
            Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections")
        )
        .Build();

    private ConnectionMultiplexer _nodeA = null!;
    private ConnectionMultiplexer _nodeB = null!;

    public async Task InitializeAsync()
    {
        await _redis.StartAsync();
        var endpoint = $"{_redis.Hostname}:{_redis.GetMappedPublicPort(6379)}";
        _nodeA = await ConnectionMultiplexer.ConnectAsync(endpoint);
        _nodeB = await ConnectionMultiplexer.ConnectAsync(endpoint);
    }

    public async Task DisposeAsync()
    {
        await _nodeA.DisposeAsync();
        await _nodeB.DisposeAsync();
        await _redis.DisposeAsync();
    }

    // La caché local NO participa en la ruta Redis: cada "nodo" tiene la suya y aun así el cupo es global.
    private static IDistributedCache LocalCache() =>
        new MemoryDistributedCache(MsOptions.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task Redis_20_solicitudes_concurrentes_misma_identidad_solo_3_obtienen_permiso()
    {
        var throttle = new PasswordResetRequestThrottle(
            LocalCache(),
            DistributedFixedWindowRateLimitConcurrencyTests.Options(Limit),
            _nodeA
        );

        var granted = await DistributedFixedWindowRateLimitConcurrencyTests.RunConcurrentlyAsync(
            () =>
                throttle.TryAcquireAsync("ana@test.com")
        );

        granted.Should().Be(Limit);
    }

    [Fact]
    public async Task Redis_dos_nodos_concurrentes_comparten_el_mismo_cupo()
    {
        var options = DistributedFixedWindowRateLimitConcurrencyTests.Options(Limit);
        var nodeA = new PasswordResetRequestThrottle(LocalCache(), options, _nodeA);
        var nodeB = new PasswordResetRequestThrottle(LocalCache(), options, _nodeB);
        var turn = 0;

        var granted = await DistributedFixedWindowRateLimitConcurrencyTests.RunConcurrentlyAsync(
            () =>
                (Interlocked.Increment(ref turn) % 2 == 0 ? nodeA : nodeB).TryAcquireAsync(
                    "multi@test.com"
                )
        );

        granted.Should().Be(Limit);
    }

    [Fact]
    public async Task Redis_la_clave_expira_con_la_ventana_y_no_contiene_el_email()
    {
        var throttle = new PasswordResetRequestThrottle(
            LocalCache(),
            DistributedFixedWindowRateLimitConcurrencyTests.Options(Limit),
            _nodeA
        );

        await throttle.TryAcquireAsync("ttl@test.com");

        var server = _nodeA.GetServer(_nodeA.GetEndPoints()[0]);
        var key = server.Keys(pattern: "erp:auth:pwreset:rl:*").Should().ContainSingle().Subject;
        key.ToString().Should().NotContain("ttl").And.NotContain("@");
        var ttl = await _nodeA.GetDatabase().KeyTimeToLiveAsync(key);
        ttl.Should().NotBeNull();
        ttl!
            .Value.Should()
            .BeGreaterThan(TimeSpan.FromMinutes(59))
            .And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(60));
    }

    // ── SEC-A01: reserva/liberación multi-clave (Lua) ─────────────────────────

    private static readonly DateTimeOffset WindowStart = new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

    private static Task<WindowReservation?> Reserve(
        IConnectionMultiplexer node,
        params (string Key, int Limit)[] counters
    ) =>
        DistributedFixedWindowRateLimit.TryReserveAllAsync(
            LocalCache(),
            node,
            counters,
            TimeSpan.FromMinutes(15),
            DateTimeOffset.UtcNow,
            CancellationToken.None
        );

    [Fact]
    public async Task Redis_reserva_rechazada_no_modifica_ningun_contador_ni_crea_claves()
    {
        var full = await Reserve(_nodeA, ("sec:b", 1));
        var window = full!.Keys[0].Split(':')[^1];

        var rejected = await Reserve(_nodeA, ("sec:a", 5), ("sec:b", 1), ("sec:c", 5));

        rejected.Should().BeNull();
        var db = _nodeA.GetDatabase();
        (await db.KeyExistsAsync($"sec:a:{window}")).Should().BeFalse();
        (await db.KeyExistsAsync($"sec:c:{window}")).Should().BeFalse();
        ((int)await db.StringGetAsync(full.Keys[0])).Should().Be(1);
    }

    [Fact]
    public async Task Redis_dos_nodos_50_reservas_concurrentes_con_limite_5_admiten_5()
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = Enumerable
            .Range(0, 50)
            .Select(i =>
                Task.Run(async () =>
                {
                    start.Wait();
                    return await Reserve(i % 2 == 0 ? _nodeA : _nodeB, ("sec:ip", 100), ("sec:pair", 5));
                })
            )
            .ToArray();
        start.Set();

        var results = await Task.WhenAll(tasks);

        results.Count(r => r is not null).Should().Be(5);
    }

    [Fact]
    public async Task Redis_liberar_decrementa_elimina_en_cero_y_nunca_queda_negativo()
    {
        var first = await Reserve(_nodeA, ("sec:rel", 5));
        var second = await Reserve(_nodeA, ("sec:rel", 5));
        var key = first!.Keys[0];
        var db = _nodeA.GetDatabase();

        await DistributedFixedWindowRateLimit.ReleaseAllAsync(LocalCache(), _nodeA, second!, DateTimeOffset.UtcNow, default);
        ((int)await db.StringGetAsync(key)).Should().Be(1);
        await DistributedFixedWindowRateLimit.ReleaseAllAsync(LocalCache(), _nodeA, first, DateTimeOffset.UtcNow, default);
        (await db.KeyExistsAsync(key)).Should().BeFalse();
        await DistributedFixedWindowRateLimit.ReleaseAllAsync(LocalCache(), _nodeA, first, DateTimeOffset.UtcNow, default);
        (await db.KeyExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Redis_liberacion_tardia_no_toca_la_ventana_siguiente_y_la_clave_expira_al_fin_de_su_ventana()
    {
        var window = TimeSpan.FromMinutes(15);
        var old = await DistributedFixedWindowRateLimit.TryReserveAllAsync(
            LocalCache(), _nodeA, [("sec:late", 5)], window, WindowStart, default);
        var current = await DistributedFixedWindowRateLimit.TryReserveAllAsync(
            LocalCache(), _nodeA, [("sec:late", 5)], window, WindowStart + window, default);
        var db = _nodeA.GetDatabase();
        await db.KeyDeleteAsync(old!.Keys[0]); // simula la expiración de la ventana anterior

        await DistributedFixedWindowRateLimit.ReleaseAllAsync(LocalCache(), _nodeA, old, WindowStart + window, default);

        current!.Keys[0].Should().NotBe(old.Keys[0]);
        ((int)await db.StringGetAsync(current.Keys[0])).Should().Be(1);
        (await db.KeyExistsAsync(old.Keys[0])).Should().BeFalse();
        var ttl = await db.KeyTimeToLiveAsync(current.Keys[0]);
        ttl.Should().NotBeNull();
        ttl!.Value.Should().BeLessThanOrEqualTo(window);
    }

    [Fact]
    public async Task Redis_login_throttle_usa_claves_sin_usuario_ni_IP_en_claro()
    {
        var throttle = new LoginAttemptThrottle(
            LocalCache(),
            MsOptions.Create(new ERP.Application.Common.Config.LoginThrottleOptions { IpLimit = 10 }),
            new LoginAttemptThrottleTests.StubHostEnvironment("Production"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LoginAttemptThrottle>.Instance,
            _nodeA
        );

        using (var attempt = await throttle.BeginAsync("Cajero.Privado", "198.51.100.77", default))
            attempt.Status.Should().Be(ERP.Application.Common.Interfaces.LoginAdmissionStatus.Admitted);

        var server = _nodeA.GetServer(_nodeA.GetEndPoints()[0]);
        var keys = server.Keys(pattern: "erp:auth:login:*").Select(k => k.ToString()).ToList();
        keys.Should().HaveCount(3);
        keys.Should().OnlyContain(k => !k.Contains("cajero", StringComparison.OrdinalIgnoreCase) && !k.Contains("198.51"));
    }

    [Fact]
    public async Task Redis_refresh_rate_limiter_tambien_es_atomico()
    {
        var limiter = new RefreshTokenRateLimiter(
            LocalCache(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RefreshTokenRateLimiter>.Instance,
            _nodeA
        );

        var granted = await DistributedFixedWindowRateLimitConcurrencyTests.RunConcurrentlyAsync(
            () =>
                limiter.TryAcquireAsync("user:42", Limit, TimeSpan.FromMinutes(1))
        );

        granted.Should().Be(Limit);
    }
}

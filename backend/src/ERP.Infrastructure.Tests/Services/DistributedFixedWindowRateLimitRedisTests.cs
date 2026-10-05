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

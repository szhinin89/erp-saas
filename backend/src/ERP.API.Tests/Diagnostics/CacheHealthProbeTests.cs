using System.Reflection;
using ERP.API.Diagnostics;
using ERP.Application.Common.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ERP.API.Tests.Diagnostics;

/// <summary>
/// ZH-API-THIN-DEVCACHE-01 — <see cref="CacheHealthProbe"/>: la conexión efímera a Redis se libera
/// siempre (antes quedaba abierta en cada request) y la semántica del reporte es la de
/// DevCacheController: fallo de connect/PING → desconectado sin ping; round-trip que falla →
/// writeReadOk=false conservando la latencia si ya se midió. El contrato HTTP completo está en
/// ERP.API.Tests/Integration/DevCacheEndpointsHttpContractTests.
/// </summary>
public sealed class CacheHealthProbeTests
{
    private const string Redis = "redis.test:6379";

    // ── Dobles mínimos (sin paquetes extra) ──────────────────────────────────

    public class InterfaceProxy<T> : DispatchProxy
        where T : class
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = (_, _) => null;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);

        public static T Create(Func<MethodInfo, object?[]?, object?> handler)
        {
            var proxy = DispatchProxy.Create<T, InterfaceProxy<T>>();
            ((InterfaceProxy<T>)(object)proxy).Handler = handler;
            return proxy;
        }
    }

    private sealed class FakeRedis
    {
        public int Connects;
        public int Disposes;
        public bool IsConnected = true;
        public Exception? ConnectError;
        public Exception? PingError;

        public Task<IConnectionMultiplexer> ConnectAsync(string connectionString)
        {
            connectionString.Should().Be(Redis);
            Connects++;
            if (ConnectError is not null)
                return Task.FromException<IConnectionMultiplexer>(ConnectError);

            var database = InterfaceProxy<IDatabase>.Create((m, _) =>
                m.Name == nameof(IDatabase.PingAsync)
                    ? PingError is null ? Task.FromResult(TimeSpan.FromMilliseconds(7)) : Task.FromException<TimeSpan>(PingError)
                    : throw new NotSupportedException(m.Name));
            var mux = InterfaceProxy<IConnectionMultiplexer>.Create((m, _) => m.Name switch
            {
                "get_IsConnected" => IsConnected,
                nameof(IConnectionMultiplexer.GetDatabase) => database,
                nameof(IAsyncDisposable.DisposeAsync) => Dispose(),
                nameof(IDisposable.Dispose) => Dispose(),
                _ => throw new NotSupportedException(m.Name),
            });
            return Task.FromResult(mux);

            object? Dispose()
            {
                Disposes++;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class ProviderStatus : ICacheProviderStatus
    {
        public string ProviderName => "Redis";
        public bool RedisConfigured => true;
        public bool FallbackActive => false;
    }

    private sealed class FailingCache(bool failOnGet, bool failOnRemove) : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));

        public byte[]? Get(string key) => throw new NotSupportedException();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            failOnGet ? throw new InvalidOperationException("get") : _inner.GetAsync(key, token);
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) =>
            _inner.SetAsync(key, value, options, token);
        public void Refresh(string key) => throw new NotSupportedException();
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => throw new NotSupportedException();
        public Task RemoveAsync(string key, CancellationToken token = default) =>
            failOnRemove ? throw new InvalidOperationException("remove") : _inner.RemoveAsync(key, token);
    }

    private static CacheHealthProbe Probe(FakeRedis redis, string? connection = Redis, IDistributedCache? cache = null) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Redis:ConnectionString"] = connection })
                .Build(),
            cache ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new ProviderStatus(),
            redis.ConnectAsync
        );

    // ── Lifecycle de la conexión ─────────────────────────────────────────────

    [Fact]
    public async Task Redis_disponible_informa_conexion_y_ping_y_libera_la_conexion()
    {
        var redis = new FakeRedis();

        var report = await Probe(redis).CheckAsync(default);

        report.RedisConfigured.Should().BeTrue();
        report.RedisConnected.Should().BeTrue();
        report.RedisPingMs.Should().Be(7);
        report.WriteReadOk.Should().BeTrue();
        report.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
        (redis.Connects, redis.Disposes).Should().Be((1, 1));
    }

    [Fact]
    public async Task Ping_fallido_informa_desconectado_sin_ping_y_libera_la_conexion()
    {
        var redis = new FakeRedis { PingError = new RedisTimeoutException("timeout", CommandStatus.Unknown) };

        var report = await Probe(redis).CheckAsync(default);

        report.RedisConnected.Should().BeFalse();
        report.RedisPingMs.Should().BeNull();
        (redis.Connects, redis.Disposes).Should().Be((1, 1));
    }

    [Fact]
    public async Task Connect_fallido_informa_desconectado_sin_conexion_que_liberar()
    {
        var redis = new FakeRedis { ConnectError = new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down") };

        var report = await Probe(redis).CheckAsync(default);

        report.RedisConfigured.Should().BeTrue();
        report.RedisConnected.Should().BeFalse();
        report.RedisPingMs.Should().BeNull();
        (redis.Connects, redis.Disposes).Should().Be((1, 0));
    }

    [Fact]
    public async Task Llamadas_repetidas_no_acumulan_conexiones_abiertas()
    {
        var redis = new FakeRedis();
        var probe = Probe(redis);

        for (var i = 0; i < 10; i++)
            await probe.CheckAsync(default);

        redis.Connects.Should().Be(10);
        redis.Disposes.Should().Be(10, "cada conexión efímera de la sonda se libera");
    }

    [Fact]
    public async Task Sin_Redis_configurado_no_intenta_conectar()
    {
        var redis = new FakeRedis();

        var report = await Probe(redis, connection: "  ").CheckAsync(default);

        report.RedisConfigured.Should().BeFalse();
        report.RedisConnected.Should().BeFalse();
        report.RedisPingMs.Should().BeNull();
        report.WriteReadOk.Should().BeTrue();
        report.InstanceName.Should().Be("ERP_");
        redis.Connects.Should().Be(0);
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Round_trip_que_falla_antes_de_medir_informa_latencia_menos_uno()
    {
        var report = await Probe(new FakeRedis(), cache: new FailingCache(failOnGet: true, failOnRemove: false)).CheckAsync(default);

        report.WriteReadOk.Should().BeFalse();
        report.LatencyMs.Should().Be(-1);
    }

    [Fact]
    public async Task Round_trip_que_falla_al_limpiar_conserva_la_latencia_medida()
    {
        var report = await Probe(new FakeRedis(), cache: new FailingCache(failOnGet: false, failOnRemove: true)).CheckAsync(default);

        report.WriteReadOk.Should().BeFalse();
        report.LatencyMs.Should().BeGreaterThanOrEqualTo(0);
    }
}

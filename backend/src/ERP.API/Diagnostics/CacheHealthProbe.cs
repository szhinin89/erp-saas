using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace ERP.API.Diagnostics;

/// <summary>
/// Sonda técnica DEV ONLY de Redis / cache distribuido (ZH-API-THIN-DEVCACHE-01, antes inline en
/// DevCacheController): detecta si Redis está configurado, abre una conexión efímera para
/// IsConnected + PING, y hace un round-trip set/get/remove sobre el <see cref="IDistributedCache"/>
/// real (Redis o memoria). Nunca lanza: cada fallo se informa en <see cref="RedisHealthReport"/>.
/// </summary>
public sealed class CacheHealthProbe
{
    private const string ProbeKey = "erp:health:probe";

    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    private readonly ICacheProviderStatus _providerStatus;
    private readonly Func<string, Task<IConnectionMultiplexer>> _connect;

    /// <remarks>
    /// <c>connect</c> es solo para tests (verificar que la conexión se libera); DI no lo registra
    /// y se usa <c>ConnectionMultiplexer.ConnectAsync</c>.
    /// </remarks>
    public CacheHealthProbe(
        IConfiguration configuration,
        IDistributedCache cache,
        ICacheProviderStatus providerStatus,
        Func<string, Task<IConnectionMultiplexer>>? connect = null
    )
    {
        _configuration = configuration;
        _cache = cache;
        _providerStatus = providerStatus;
        _connect = connect ?? (async cs => await ConnectionMultiplexer.ConnectAsync(cs));
    }

    public async Task<RedisHealthReport> CheckAsync(CancellationToken cancellationToken)
    {
        var redisConnection =
            _configuration["Redis:ConnectionString"] ?? _configuration.GetConnectionString("Redis");
        var redisConfigured = !string.IsNullOrWhiteSpace(redisConnection);

        var (redisConnected, redisPingMs) = redisConfigured
            ? await ProbeRedisAsync(redisConnection!)
            : (false, null);
        var (writeReadOk, latencyMs) = await RoundTripAsync(cancellationToken);

        return new RedisHealthReport(
            redisConfigured,
            redisConfigured && redisConnected,
            _providerStatus.FallbackActive,
            _providerStatus.ProviderName,
            writeReadOk,
            latencyMs,
            redisPingMs,
            _configuration["Redis:InstanceName"] ?? "ERP_"
        );
    }

    /// <summary>
    /// Conexión propia y efímera (no hay un IConnectionMultiplexer compartido en DI: el de
    /// IDistributedCache es interno de StackExchangeRedisCache). Se libera siempre; antes quedaba
    /// abierta en cada request. Si connect o PING fallan: desconectado y sin ping.
    /// </summary>
    private async Task<(bool Connected, long? PingMs)> ProbeRedisAsync(string connectionString)
    {
        IConnectionMultiplexer? mux = null;
        try
        {
            mux = await _connect(connectionString);
            var connected = mux.IsConnected;
            var ping = await mux.GetDatabase().PingAsync();
            return (connected, (long)ping.TotalMilliseconds);
        }
        catch
        {
            return (false, null);
        }
        finally
        {
            if (mux is not null)
                await DisposeQuietlyAsync(mux);
        }
    }

    private static async Task DisposeQuietlyAsync(IConnectionMultiplexer mux)
    {
        try
        {
            await mux.DisposeAsync();
        }
        catch
        {
            // Cerrar la sonda nunca altera el resultado informado.
        }
    }

    /// <summary>Latencia de set+get; si cualquier paso falla: writeReadOk=false (latencia -1 si no llegó a medirse).</summary>
    private async Task<(bool WriteReadOk, long LatencyMs)> RoundTripAsync(
        CancellationToken cancellationToken
    )
    {
        var probeValue = Guid.NewGuid().ToString("N");
        long latencyMs = -1;
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _cache.SetStringAsync(
                ProbeKey,
                probeValue,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15),
                },
                cancellationToken
            );
            var read = await _cache.GetStringAsync(ProbeKey, cancellationToken);
            sw.Stop();
            latencyMs = sw.ElapsedMilliseconds;
            var writeReadOk = string.Equals(read, probeValue, StringComparison.Ordinal);
            await _cache.RemoveAsync(ProbeKey, cancellationToken);
            return (writeReadOk, latencyMs);
        }
        catch
        {
            return (false, latencyMs);
        }
    }
}

/// <summary>Respuesta de /api/dev/redis-health (el orden de las propiedades es el del JSON).</summary>
public sealed record RedisHealthReport(
    bool RedisConfigured,
    bool RedisConnected,
    bool FallbackActive,
    string Provider,
    bool WriteReadOk,
    long LatencyMs,
    long? RedisPingMs,
    string InstanceName
);

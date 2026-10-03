using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Globalization;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Ventana fija con incremento ATÓMICO por clave. Lógica única de <see cref="RefreshTokenRateLimiter"/>
/// y <see cref="PasswordResetRequestThrottle"/>; cada consumidor define su propia clave.
/// <para>
/// <see cref="IDistributedCache"/> no ofrece una operación atómica de lectura-modificación-escritura
/// (Get y Set son dos llamadas independientes: N solicitudes concurrentes leían el mismo contador y
/// todas obtenían permiso). Por eso:
/// </para>
/// <list type="bullet">
/// <item>Con Redis (<see cref="IConnectionMultiplexer"/> registrado): un script Lua
/// <c>INCR</c> + <c>PEXPIRE</c> se ejecuta como una sola operación atómica en el servidor, válida
/// entre procesos y nodos.</item>
/// <item>Sin Redis (fallback <see cref="IDistributedCache"/> en memoria, que no se comparte entre
/// instancias): la lectura-escritura se serializa por clave con locks process-local particionados.</item>
/// </list>
/// </summary>
internal static class DistributedFixedWindowRateLimit
{
    // Primer INCR fija la expiración: la ventana arranca con la primera solicitud (misma semántica
    // que el contador en caché). Devuelve 1 si la solicitud entra en el cupo, 0 si lo excede.
    private const string RedisScript =
        "local count = redis.call('INCR', KEYS[1]) "
        + "if count == 1 then redis.call('PEXPIRE', KEYS[1], ARGV[1]) end "
        + "if count > tonumber(ARGV[2]) then return 0 end "
        + "return 1";

    // Locks particionados (no uno por clave): memoria acotada aunque lleguen claves arbitrarias.
    private static readonly SemaphoreSlim[] Stripes = Enumerable
        .Range(0, 64)
        .Select(_ => new SemaphoreSlim(1, 1))
        .ToArray();

    public static async Task<bool> TryAcquireAsync(
        IDistributedCache cache,
        IConnectionMultiplexer? redis,
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken
    )
    {
        if (redis is not null)
            return await TryAcquireRedisAsync(redis, key, limit, window);

        var stripe = Stripes[(int)((uint)StringComparer.Ordinal.GetHashCode(key) % (uint)Stripes.Length)];
        await stripe.WaitAsync(cancellationToken);
        try
        {
            return await TryAcquireCacheAsync(cache, key, limit, window, cancellationToken);
        }
        finally
        {
            stripe.Release();
        }
    }

    private static async Task<bool> TryAcquireRedisAsync(
        IConnectionMultiplexer redis,
        string key,
        int limit,
        TimeSpan window
    )
    {
        var result = await redis
            .GetDatabase()
            .ScriptEvaluateAsync(
                RedisScript,
                [new RedisKey(key)],
                [(long)window.TotalMilliseconds, limit]
            );
        return (int)result == 1;
    }

    private static async Task<bool> TryAcquireCacheAsync(
        IDistributedCache cache,
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken
    )
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = (long)window.TotalMilliseconds;

        var raw = await cache.GetStringAsync(key, cancellationToken);
        Bucket bucket;
        if (string.IsNullOrEmpty(raw))
        {
            bucket = new Bucket(now, 1);
        }
        else
        {
            var parts = raw.Split(':');
            var windowStart = long.Parse(parts[0], CultureInfo.InvariantCulture);
            var count = int.Parse(parts[1], CultureInfo.InvariantCulture);

            if (now - windowStart >= windowMs)
                bucket = new Bucket(now, 1);
            else if (count >= limit)
                return false;
            else
                bucket = new Bucket(windowStart, count + 1);
        }

        await cache.SetStringAsync(
            key,
            $"{bucket.WindowStartMs}:{bucket.Count}",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window },
            cancellationToken
        );

        return true;
    }

    private readonly record struct Bucket(long WindowStartMs, int Count);
}

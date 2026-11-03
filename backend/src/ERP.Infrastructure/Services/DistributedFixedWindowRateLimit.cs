using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

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
/// <para>
/// SEC-A01 — <see cref="TryReserveAllAsync"/>/<see cref="ReleaseAllAsync"/>: reserva de varios
/// contadores a la vez, sin consumo parcial. Las claves llevan el número de ventana (alineada a múltiplos de
/// la duración), así que una liberación tardía cae sobre una clave ya expirada y nunca toca la
/// ventana siguiente. <see cref="TryAcquireAsync"/> no cambia.
/// </para>
/// </summary>
public static class DistributedFixedWindowRateLimit
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

        var stripe = Stripes[
            (int)((uint)StringComparer.Ordinal.GetHashCode(key) % (uint)Stripes.Length)
        ];
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

    // Comprueba TODOS los contadores antes de escribir: si uno está lleno no se modifica ninguno.
    // ARGV[1] = ms hasta el fin de la ventana; ARGV[i+1] = límite de KEYS[i].
    private const string ReserveAllScript =
        "for i = 1, #KEYS do "
        + "local count = tonumber(redis.call('GET', KEYS[i]) or '0') "
        + "if count >= tonumber(ARGV[i + 1]) then return 0 end "
        + "end "
        + "for i = 1, #KEYS do "
        + "if redis.call('INCR', KEYS[i]) == 1 then redis.call('PEXPIRE', KEYS[i], ARGV[1]) end "
        + "end "
        + "return 1";

    // Nunca negativo: decrementa si queda más de 1 y elimina la clave al llegar a 0.
    private const string ReleaseAllScript =
        "for i = 1, #KEYS do "
        + "local count = tonumber(redis.call('GET', KEYS[i]) or '0') "
        + "if count > 1 then redis.call('DECR', KEYS[i]) "
        + "elseif count == 1 then redis.call('DEL', KEYS[i]) end "
        + "end "
        + "return 1";

    // Fallback en memoria: la operación multi-clave completa bajo un único lock (instancia única).
    private static readonly SemaphoreSlim ReservationGate = new(1, 1);

    /// <summary>
    /// Reserva un cupo en cada contador solo si todos lo admiten; si alguno está lleno devuelve
    /// <c>null</c> sin modificar ninguno. Devuelve las claves con ventana para <see cref="ReleaseAllAsync"/>.
    /// Con Redis, los errores de conexión se propagan (el consumidor decide la respuesta).
    /// </summary>
    public static async Task<WindowReservation?> TryReserveAllAsync(
        IDistributedCache cache,
        IConnectionMultiplexer? redis,
        IReadOnlyList<(string Key, int Limit)> counters,
        TimeSpan window,
        DateTime nowUtc,
        CancellationToken cancellationToken
    )
    {
        var windowMs = (long)window.TotalMilliseconds;
        var nowMs = UnixMilliseconds(nowUtc);
        var windowId = nowMs / windowMs;
        var windowEndMs = (windowId + 1) * windowMs;
        var keys = counters
            .Select(c => $"{c.Key}:{windowId.ToString(CultureInfo.InvariantCulture)}")
            .ToArray();
        var reservation = new WindowReservation(keys, windowEndMs);

        if (redis is not null)
        {
            var args = new RedisValue[counters.Count + 1];
            args[0] = Math.Max(1, windowEndMs - nowMs);
            for (var i = 0; i < counters.Count; i++)
                args[i + 1] = counters[i].Limit;
            var result = await redis
                .GetDatabase()
                .ScriptEvaluateAsync(
                    ReserveAllScript,
                    keys.Select(k => new RedisKey(k)).ToArray(),
                    args
                );
            return (int)result == 1 ? reservation : null;
        }

        await ReservationGate.WaitAsync(cancellationToken);
        try
        {
            var counts = new int[keys.Length];
            for (var i = 0; i < keys.Length; i++)
            {
                counts[i] = await ReadCountAsync(cache, keys[i], cancellationToken);
                if (counts[i] >= counters[i].Limit)
                    return null;
            }

            var expiry = ExpiryUntil(windowEndMs, nowMs);
            for (var i = 0; i < keys.Length; i++)
                await cache.SetStringAsync(
                    keys[i],
                    (counts[i] + 1).ToString(CultureInfo.InvariantCulture),
                    expiry,
                    cancellationToken
                );
            return reservation;
        }
        finally
        {
            ReservationGate.Release();
        }
    }

    /// <summary>
    /// Devuelve el cupo de cada clave de la reserva (nunca por debajo de 0). Si la ventana ya expiró
    /// no hace nada: la clave de la ventana siguiente tiene otro nombre.
    /// </summary>
    public static async Task ReleaseAllAsync(
        IDistributedCache cache,
        IConnectionMultiplexer? redis,
        WindowReservation reservation,
        DateTime nowUtc,
        CancellationToken cancellationToken
    )
    {
        if (redis is not null)
        {
            await redis
                .GetDatabase()
                .ScriptEvaluateAsync(
                    ReleaseAllScript,
                    reservation.Keys.Select(k => new RedisKey(k)).ToArray()
                );
            return;
        }

        await ReservationGate.WaitAsync(cancellationToken);
        try
        {
            var nowMs = UnixMilliseconds(nowUtc);
            if (nowMs >= reservation.WindowEndMs)
                return;

            var expiry = ExpiryUntil(reservation.WindowEndMs, nowMs);
            foreach (var key in reservation.Keys)
            {
                var count = await ReadCountAsync(cache, key, cancellationToken);
                if (count > 1)
                    await cache.SetStringAsync(
                        key,
                        (count - 1).ToString(CultureInfo.InvariantCulture),
                        expiry,
                        cancellationToken
                    );
                else if (count == 1)
                    await cache.RemoveAsync(key, cancellationToken);
            }
        }
        finally
        {
            ReservationGate.Release();
        }
    }

    // Instante UTC → ms Unix sin conversiones de zona horaria (Kind distinto de Utc = error).
    private static long UnixMilliseconds(DateTime nowUtc) =>
        nowUtc.Kind == DateTimeKind.Utc
            ? (nowUtc - DateTime.UnixEpoch).Ticks / TimeSpan.TicksPerMillisecond
            : throw new ArgumentException("Se requiere DateTime con Kind=Utc.", nameof(nowUtc));

    private static DistributedCacheEntryOptions ExpiryUntil(long windowEndMs, long nowMs) =>
        new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(
                Math.Max(1, windowEndMs - nowMs)
            ),
        };

    private static async Task<int> ReadCountAsync(
        IDistributedCache cache,
        string key,
        CancellationToken cancellationToken
    )
    {
        var raw = await cache.GetStringAsync(key, cancellationToken);
        return string.IsNullOrEmpty(raw) ? 0 : int.Parse(raw, CultureInfo.InvariantCulture);
    }
}

/// <summary>Claves con ventana reservadas por <see cref="DistributedFixedWindowRateLimit.TryReserveAllAsync"/>.</summary>
public sealed record WindowReservation(IReadOnlyList<string> Keys, long WindowEndMs);

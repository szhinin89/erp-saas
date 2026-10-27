using System.Globalization;
using StackExchange.Redis;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// SEC-06 — versiones y marcas "invalidación en curso" de la caché de permisos efectivos, con
/// operaciones ATÓMICAS. <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>
/// no ofrece lectura-modificación-escritura atómica (el bump GET+1+SET perdía incrementos
/// concurrentes), mismo motivo y mismo patrón que <c>DistributedFixedWindowRateLimit</c>:
/// <list type="bullet">
/// <item>Con Redis (<see cref="IConnectionMultiplexer"/> registrado): scripts Lua, atómicos en el
/// servidor y compartidos por todas las instancias. Claves con hash tag común (mismo slot en
/// Redis Cluster), sin expiración las versiones; una versión ausente (p. ej. desalojada) se
/// re-inicializa con <see cref="DateTime.UtcNow"/> en ticks, mayor que cualquier versión previa,
/// para que una entrada antigua nunca vuelva a coincidir.</item>
/// <item>Sin Redis (caché en memoria, solo despliegue de instancia única): contadores del proceso
/// bajo un lock; la caché de datos en memoria vive y muere con el mismo proceso.</item>
/// </list>
/// Las marcas de Begin expiran a los <see cref="PendingExpiry"/>: mayor que
/// <see cref="DistributedPermissionsCacheService.MaxStampAge"/> + TTL de entrada, de modo que
/// cuando una marca huérfana (Complete fallido) expira ya no puede quedar ninguna entrada
/// publicada con datos anteriores a la mutación.
/// </summary>
public sealed class PermissionsCacheVersionStore
{
    public static readonly TimeSpan PendingExpiry = TimeSpan.FromSeconds(900);

    private const string KeyPrefix = "{erp:permissions}";

    // KEYS = n claves de versión seguidas de n claves de marca; ARGV[1] = valor inicial.
    // Devuelve { pendiente (0/1), versión_1, ..., versión_n } (versiones como texto: exceden 2^53).
    private const string ReadScript =
        "local n = #KEYS / 2 "
        + "local res = { 0 } "
        + "for i = 1, n do "
        + "  local v = redis.call('GET', KEYS[i]) "
        + "  if not v then redis.call('SET', KEYS[i], ARGV[1], 'NX') v = redis.call('GET', KEYS[i]) end "
        + "  res[i + 1] = v "
        + "  local p = tonumber(redis.call('GET', KEYS[n + i]) or '0') "
        + "  if p > 0 then res[1] = 1 end "
        + "end "
        + "return res";

    // KEYS[1] = marca; ARGV[1] = expiración en ms.
    private const string BeginScript =
        "redis.call('INCR', KEYS[1]) " + "redis.call('PEXPIRE', KEYS[1], ARGV[1]) " + "return 1";

    // KEYS[1] = versión, KEYS[2] = marca; ARGV[1] = valor inicial. Versión primero, marca después,
    // en el mismo script: nunca se retira la marca sin haber incrementado la versión.
    private const string CompleteScript =
        "if redis.call('EXISTS', KEYS[1]) == 0 then redis.call('SET', KEYS[1], ARGV[1]) end "
        + "redis.call('INCR', KEYS[1]) "
        + "local p = redis.call('DECR', KEYS[2]) "
        + "if p <= 0 then redis.call('DEL', KEYS[2]) end "
        + "return 1";

    private readonly IConnectionMultiplexer? _redis;
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _versions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _pending = new(StringComparer.Ordinal);

    public PermissionsCacheVersionStore(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    /// <summary>Versiones de los alcances, en orden, y si alguno tiene una invalidación en curso.</summary>
    public async Task<(bool Pending, long[] Versions)> ReadAsync(IReadOnlyList<string> scopeKeys)
    {
        if (_redis is null)
        {
            lock (_gate)
            {
                var pending = scopeKeys.Any(k => _pending.GetValueOrDefault(k) > 0);
                return (pending, scopeKeys.Select(k => _versions.GetValueOrDefault(k)).ToArray());
            }
        }

        var keys = scopeKeys
            .Select(VersionKey)
            .Concat(scopeKeys.Select(PendingKey))
            .Select(k => new RedisKey(k))
            .ToArray();
        var result = (RedisResult[])(
            await _redis.GetDatabase().ScriptEvaluateAsync(ReadScript, keys, [InitialVersion()])
        )!;

        var versions = result
            .Skip(1)
            .Select(v => long.Parse((string)v!, CultureInfo.InvariantCulture))
            .ToArray();
        return ((long)result[0] > 0, versions);
    }

    public async Task BeginAsync(string scopeKey)
    {
        if (_redis is null)
        {
            lock (_gate)
                _pending[scopeKey] = _pending.GetValueOrDefault(scopeKey) + 1;
            return;
        }

        await _redis
            .GetDatabase()
            .ScriptEvaluateAsync(
                BeginScript,
                [new RedisKey(PendingKey(scopeKey))],
                [(long)PendingExpiry.TotalMilliseconds]
            );
    }

    public async Task CompleteAsync(string scopeKey)
    {
        if (_redis is null)
        {
            lock (_gate)
            {
                _versions[scopeKey] = _versions.GetValueOrDefault(scopeKey) + 1;
                var remaining = _pending.GetValueOrDefault(scopeKey) - 1;
                if (remaining > 0)
                    _pending[scopeKey] = remaining;
                else
                    _pending.Remove(scopeKey);
            }
            return;
        }

        await _redis
            .GetDatabase()
            .ScriptEvaluateAsync(
                CompleteScript,
                [new RedisKey(VersionKey(scopeKey)), new RedisKey(PendingKey(scopeKey))],
                [InitialVersion()]
            );
    }

    public static string VersionKey(string scopeKey) => $"{KeyPrefix}:v:{scopeKey}";

    public static string PendingKey(string scopeKey) => $"{KeyPrefix}:p:{scopeKey}";

    private static RedisValue InitialVersion() =>
        DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
}

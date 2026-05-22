using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Caching;

/// <summary>
/// Decorador no invasivo sobre IDistributedCache: métricas + logs selectivos para auditoría.
/// </summary>
public sealed class InstrumentedDistributedCache : IDistributedCache
{
    private static readonly string[] LogKeyPrefixes =
    [
        "permissions:",
        "entitlements:",
        "Query:",
        "erp:refresh:rl:",
    ];

    private readonly IDistributedCache _inner;
    private readonly ICacheDiagnosticsMetrics _metrics;
    private readonly CacheDiagnosticsOptions _options;
    private readonly ILogger<InstrumentedDistributedCache> _logger;

    public InstrumentedDistributedCache(
        IDistributedCache inner,
        ICacheDiagnosticsMetrics metrics,
        IOptions<CacheDiagnosticsOptions> options,
        ILogger<InstrumentedDistributedCache> logger)
    {
        _inner = inner;
        _metrics = metrics;
        _options = options.Value;
        _logger = logger;
    }

    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var bytes = await _inner.GetAsync(key, token);
        var category = ClassifyKey(key);
        if (bytes is { Length: > 0 })
        {
            _metrics.RecordHit(category);
            LogIfAllowed("CACHE HIT", key, category);
        }
        else
        {
            _metrics.RecordMiss(category);
            LogIfAllowed("CACHE MISS", key, category);
        }

        return bytes;
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        => SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(
        string key,
        byte[] value,
        DistributedCacheEntryOptions options,
        CancellationToken token = default)
    {
        await _inner.SetAsync(key, value, options, token);
        _metrics.RecordSet(ClassifyKey(key));
        LogIfAllowed("CACHE SET", key, ClassifyKey(key));
    }

    public void Refresh(string key) => RefreshAsync(key).GetAwaiter().GetResult();

    public Task RefreshAsync(string key, CancellationToken token = default)
        => _inner.RefreshAsync(key, token);

    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public Task RemoveAsync(string key, CancellationToken token = default)
        => _inner.RemoveAsync(key, token);

    private void LogIfAllowed(string action, string key, string category)
    {
        if (!_options.LogEnabled || !ShouldLogKey(key))
            return;

        _logger.LogInformation("{Action} category={Category} key={Key}", action, category, key);
    }

    private static bool ShouldLogKey(string key)
    {
        foreach (var prefix in LogKeyPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    internal static string ClassifyKey(string key)
    {
        if (key.StartsWith("permissions:", StringComparison.Ordinal))
            return "permissions";
        if (key.StartsWith("entitlements:", StringComparison.Ordinal))
            return "entitlements";
        if (key.StartsWith("Query:", StringComparison.Ordinal))
            return "query";
        if (key.StartsWith("erp:refresh:rl:", StringComparison.Ordinal))
            return "refresh-rate-limit";
        if (key.StartsWith("erp:health:", StringComparison.Ordinal))
            return "health-probe";
        return "other";
    }
}

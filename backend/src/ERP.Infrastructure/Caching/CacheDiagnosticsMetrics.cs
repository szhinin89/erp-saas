using System.Collections.Concurrent;
using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Caching;

public sealed class CacheDiagnosticsMetrics : ICacheDiagnosticsMetrics
{
    private long _hits;
    private long _misses;
    private long _sets;
    private readonly ConcurrentDictionary<string, long> _hitsByCategory = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _missesByCategory = new(StringComparer.Ordinal);

    public void RecordHit(string category)
    {
        Interlocked.Increment(ref _hits);
        _hitsByCategory.AddOrUpdate(Normalize(category), 1, static (_, c) => c + 1);
    }

    public void RecordMiss(string category)
    {
        Interlocked.Increment(ref _misses);
        _missesByCategory.AddOrUpdate(Normalize(category), 1, static (_, c) => c + 1);
    }

    public void RecordSet(string category)
    {
        Interlocked.Increment(ref _sets);
    }

    public CacheMetricsSnapshot GetSnapshot()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var sets = Interlocked.Read(ref _sets);
        var total = hits + misses;
        var ratio = total == 0 ? 0 : Math.Round(hits / (double)total, 4);

        return new CacheMetricsSnapshot(
            hits,
            misses,
            sets,
            ratio,
            _hitsByCategory.ToDictionary(static kv => kv.Key, static kv => kv.Value),
            _missesByCategory.ToDictionary(static kv => kv.Key, static kv => kv.Value));
    }

    private static string Normalize(string category)
        => string.IsNullOrWhiteSpace(category) ? "unknown" : category.Trim().ToLowerInvariant();
}

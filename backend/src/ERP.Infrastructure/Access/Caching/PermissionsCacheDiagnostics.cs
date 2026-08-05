using System.Collections.Concurrent;
using ERP.Application.Access.Caching;

namespace ERP.Infrastructure.Access.Caching;

public sealed class PermissionsCacheDiagnostics : IPermissionsCacheDiagnostics
{
    private long _hits;
    private long _misses;
    private long _sets;
    private long _errors;
    private readonly ConcurrentDictionary<string, long> _missesByReason = new(
        StringComparer.Ordinal
    );

    public void RecordHit() => Interlocked.Increment(ref _hits);

    public void RecordMiss(PermissionsCacheMissReason reason)
    {
        Interlocked.Increment(ref _misses);
        _missesByReason.AddOrUpdate(reason.ToString(), 1, static (_, c) => c + 1);
    }

    public void RecordSet() => Interlocked.Increment(ref _sets);

    public void RecordError() => Interlocked.Increment(ref _errors);

    public PermissionsCacheDiagnosticsSnapshot GetSnapshot()
    {
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var total = hits + misses;
        var ratio = total == 0 ? 0 : Math.Round(hits / (double)total, 4);

        return new PermissionsCacheDiagnosticsSnapshot(
            hits,
            misses,
            Interlocked.Read(ref _sets),
            Interlocked.Read(ref _errors),
            ratio,
            _missesByReason.ToDictionary(static kv => kv.Key, static kv => kv.Value)
        );
    }
}

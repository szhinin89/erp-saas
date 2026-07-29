namespace ERP.Application.Access.Caching;

public interface IPermissionsCacheDiagnostics
{
    void RecordHit();
    void RecordMiss(PermissionsCacheMissReason reason);
    void RecordSet();
    void RecordError();

    PermissionsCacheDiagnosticsSnapshot GetSnapshot();
}

public sealed record PermissionsCacheDiagnosticsSnapshot(
    long CacheHitTotal,
    long CacheMissTotal,
    long CacheSetTotal,
    long CacheErrorTotal,
    double HitRatio,
    IReadOnlyDictionary<string, long> MissesByReason
);

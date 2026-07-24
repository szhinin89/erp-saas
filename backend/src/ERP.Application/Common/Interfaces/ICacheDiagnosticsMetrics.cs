namespace ERP.Application.Common.Interfaces;

/// <summary>Contadores in-process de cache para auditoría (hits/misses/sets).</summary>
public interface ICacheDiagnosticsMetrics
{
    void RecordHit(string category);
    void RecordMiss(string category);
    void RecordSet(string category);

    CacheMetricsSnapshot GetSnapshot();
}

public sealed record CacheMetricsSnapshot(
    long Hits,
    long Misses,
    long Sets,
    double HitRatio,
    IReadOnlyDictionary<string, long> HitsByCategory,
    IReadOnlyDictionary<string, long> MissesByCategory);

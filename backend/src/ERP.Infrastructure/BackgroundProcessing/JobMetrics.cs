using System.Diagnostics.Metrics;

namespace ERP.Infrastructure.BackgroundProcessing;

public static class JobMetrics
{
    public static readonly Meter Meter = new("ERP.Jobs");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("erp.jobs.duration", "s");
    public static readonly Counter<long> Processed = Meter.CreateCounter<long>("erp.jobs.processed");
    public static readonly Counter<long> Failed = Meter.CreateCounter<long>("erp.jobs.failed");
    public static readonly Counter<long> LockSkipped = Meter.CreateCounter<long>("erp.jobs.lock_skipped");
    public static readonly Gauge<long> Backlog = Meter.CreateGauge<long>("erp.jobs.backlog");
    public static readonly Gauge<double> OldestAge = Meter.CreateGauge<double>("erp.jobs.oldest_pending_age", "s");
    private static readonly AsyncLocal<string?> Current = new();
    public static IDisposable Begin(string id)
    {
        var previous = Current.Value;
        Current.Value = id;
        return new Reset(previous);
    }
    public static void RecordProcessed(long count = 1) => Processed.Add(count, Tag());
    public static void RecordFailed(long count = 1) => Failed.Add(count, Tag());
    public static void RecordBacklog(long count, DateTime? oldest, string stream = "main")
    {
        Backlog.Record(count, Tag(), new KeyValuePair<string, object?>("stream", stream));
        OldestAge.Record(oldest.HasValue ? Math.Max(0, (DateTime.UtcNow - oldest.Value).TotalSeconds) : 0,
            Tag(), new KeyValuePair<string, object?>("stream", stream));
    }
    private static KeyValuePair<string, object?> Tag() => new("job", Current.Value ?? "direct");
    private sealed class Reset(string? previous) : IDisposable { public void Dispose() => Current.Value = previous; }
}

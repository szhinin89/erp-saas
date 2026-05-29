using System.Diagnostics.Metrics;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// OpenTelemetry-compatible metrics for the subscription lifecycle engine.
/// Registered as Singleton — Meter is thread-safe.
/// </summary>
public sealed class SubscriptionMetrics : IDisposable
{
    public const string MeterName = "ERP.Subscription";

    private readonly Meter _meter;

    // ── Cache ──────────────────────────────────────────────────────────────────
    public readonly Counter<long> AccessCacheHits;
    public readonly Counter<long> AccessCacheMisses;

    // ── Access decisions ───────────────────────────────────────────────────────
    public readonly Counter<long> AccessDeniedTotal;
    public readonly Counter<long> AccessAllowedTotal;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    public readonly Counter<long> SubscriptionSuspendTotal;
    public readonly Counter<long> TrialExpiredTotal;
    public readonly Counter<long> GracePeriodStartedTotal;

    // ── Performance ────────────────────────────────────────────────────────────
    public readonly Histogram<double> LifecycleTransitionDurationMs;
    public readonly Histogram<double> SnapshotRebuildDurationMs;

    public SubscriptionMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        AccessCacheHits    = _meter.CreateCounter<long>("subscription_access_cache_hits",   description: "Cache hits on subscription access snapshot");
        AccessCacheMisses  = _meter.CreateCounter<long>("subscription_access_cache_misses", description: "Cache misses — snapshot rebuilt from DB");
        AccessDeniedTotal  = _meter.CreateCounter<long>("subscription_access_denied_total", description: "Requests blocked by subscription access middleware");
        AccessAllowedTotal = _meter.CreateCounter<long>("subscription_access_allowed_total",description: "Requests allowed by subscription access middleware");

        SubscriptionSuspendTotal    = _meter.CreateCounter<long>("subscription_suspend_total",         description: "Total suspension transitions");
        TrialExpiredTotal           = _meter.CreateCounter<long>("subscription_trial_expired_total",   description: "Total trial expiry transitions");
        GracePeriodStartedTotal     = _meter.CreateCounter<long>("subscription_grace_period_started",  description: "Total grace period starts");

        LifecycleTransitionDurationMs = _meter.CreateHistogram<double>("lifecycle_transition_duration_ms", unit: "ms", description: "Duration of lifecycle orchestrator transitions");
        SnapshotRebuildDurationMs     = _meter.CreateHistogram<double>("snapshot_rebuild_duration_ms",     unit: "ms", description: "Duration of DB snapshot rebuilds");
    }

    public void Dispose() => _meter.Dispose();
}

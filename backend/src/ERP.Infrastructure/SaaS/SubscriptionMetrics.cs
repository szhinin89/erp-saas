using System.Diagnostics.Metrics;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// OpenTelemetry-compatible metrics for the subscription lifecycle + billing engine.
/// Registered as Singleton — Meter is thread-safe.
/// </summary>
public sealed class SubscriptionMetrics : IDisposable
{
    public const string MeterName = "ERP.Subscription";

    private readonly Meter _meter;

    // ── Subscriber counts (observable — polled) ────────────────────────────────
    // These use UpDownCounter so dashboards can show live totals.
    public readonly UpDownCounter<long> ActiveSubscribersTotal;
    public readonly UpDownCounter<long> TrialSubscribersTotal;
    public readonly UpDownCounter<long> SuspendedSubscribersTotal;

    // ── Cache ──────────────────────────────────────────────────────────────────
    public readonly Counter<long> AccessCacheHits;
    public readonly Counter<long> AccessCacheMisses;

    // ── Access decisions ───────────────────────────────────────────────────────
    public readonly Counter<long> AccessDeniedTotal;
    public readonly Counter<long> AccessAllowedTotal;

    // ── Lifecycle transitions ──────────────────────────────────────────────────
    public readonly Counter<long> SubscriptionSuspendTotal;
    public readonly Counter<long> TrialExpiredTotal;
    public readonly Counter<long> GracePeriodStartedTotal;
    public readonly Counter<long> GracePeriodRecoveredTotal;

    // ── Renewal ────────────────────────────────────────────────────────────────
    public readonly Counter<long> RenewalSuccessTotal;
    public readonly Counter<long> RenewalFailureTotal;

    // ── Invoices ───────────────────────────────────────────────────────────────
    public readonly Counter<long> InvoicesGeneratedTotal;
    public readonly Counter<long> InvoicesPaidTotal;

    // ── Payment attempts ───────────────────────────────────────────────────────
    public readonly Counter<long> PaymentAttemptsTotal;
    public readonly Counter<long> PaymentAttemptsFailedTotal;

    // ── Checkout ───────────────────────────────────────────────────────────────
    public readonly Counter<long> CheckoutSessionsTotal;

    // ── Performance ────────────────────────────────────────────────────────────
    public readonly Histogram<double> LifecycleTransitionDurationMs;
    public readonly Histogram<double> SnapshotRebuildDurationMs;
    public readonly Histogram<double> RenewalProcessingDurationMs;

    public SubscriptionMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");

        // Subscriber totals
        ActiveSubscribersTotal    = _meter.CreateUpDownCounter<long>("active_subscribers_total",    description: "Currently active subscribers");
        TrialSubscribersTotal     = _meter.CreateUpDownCounter<long>("trial_subscribers_total",     description: "Subscribers in trial");
        SuspendedSubscribersTotal = _meter.CreateUpDownCounter<long>("suspended_subscribers_total", description: "Currently suspended subscribers");

        // Cache
        AccessCacheHits   = _meter.CreateCounter<long>("subscription_access_cache_hits",   description: "Cache hits on subscription access snapshot");
        AccessCacheMisses = _meter.CreateCounter<long>("subscription_access_cache_misses", description: "Cache misses — snapshot rebuilt from DB");

        // Access decisions
        AccessDeniedTotal  = _meter.CreateCounter<long>("subscription_access_denied_total",  description: "Requests blocked by subscription access middleware");
        AccessAllowedTotal = _meter.CreateCounter<long>("subscription_access_allowed_total", description: "Requests allowed");

        // Lifecycle
        SubscriptionSuspendTotal    = _meter.CreateCounter<long>("subscription_suspend_total",          description: "Total suspension transitions");
        TrialExpiredTotal           = _meter.CreateCounter<long>("subscription_trial_expired_total",    description: "Total trial expiry transitions");
        GracePeriodStartedTotal     = _meter.CreateCounter<long>("grace_period_entered_total",          description: "Total grace period entries");
        GracePeriodRecoveredTotal   = _meter.CreateCounter<long>("grace_period_recovered_total",        description: "Grace periods recovered via payment");

        // Renewal
        RenewalSuccessTotal = _meter.CreateCounter<long>("renewal_success_total", description: "Successful subscription renewals");
        RenewalFailureTotal = _meter.CreateCounter<long>("renewal_failure_total", description: "Failed subscription renewals");

        // Invoices
        InvoicesGeneratedTotal = _meter.CreateCounter<long>("invoices_generated_total", description: "SaaS invoices created");
        InvoicesPaidTotal      = _meter.CreateCounter<long>("invoices_paid_total",      description: "SaaS invoices marked paid");

        // Payment attempts
        PaymentAttemptsTotal       = _meter.CreateCounter<long>("payment_attempts_total",        description: "Total payment charge attempts");
        PaymentAttemptsFailedTotal = _meter.CreateCounter<long>("payment_attempts_failed_total", description: "Failed payment charge attempts");

        // Checkout
        CheckoutSessionsTotal = _meter.CreateCounter<long>("checkout_sessions_total", description: "Checkout sessions created");

        // Performance
        LifecycleTransitionDurationMs = _meter.CreateHistogram<double>("lifecycle_transition_duration_ms", unit: "ms");
        SnapshotRebuildDurationMs     = _meter.CreateHistogram<double>("snapshot_rebuild_duration_ms",     unit: "ms");
        RenewalProcessingDurationMs   = _meter.CreateHistogram<double>("renewal_processing_duration_ms",   unit: "ms");
    }

    public void Dispose() => _meter.Dispose();
}

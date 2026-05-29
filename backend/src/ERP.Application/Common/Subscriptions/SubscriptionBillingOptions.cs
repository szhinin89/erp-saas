namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Centralized configurable constants for the SaaS subscription billing flow.
/// All magic numbers are now in one place — no more scattered hardcoded values.
///
/// Configure via appsettings.json:
/// {
///   "SubscriptionBilling": {
///     "TrialGracePeriodDays": 7,
///     "RenewalGracePeriodDays": 7,
///     "WebhookPaymentFailureGracePeriodDays": 7,
///     "TrialDurationDays": 14,
///     "InvoiceDueDays": 30,
///     "RenewalLookAheadHours": 24,
///     "AccessCacheL1TtlSeconds": 20,
///     "AccessCacheL2TtlMinutes": 5,
///     "AccessCacheVersionTtlDays": 30
///   }
/// }
/// </summary>
public sealed class SubscriptionBillingOptions
{
    public const string SectionName = "SubscriptionBilling";

    // ── Grace periods ─────────────────────────────────────────────────────────

    /// <summary>Days of grace period granted when a trial expires without payment. Default: 7.</summary>
    public int TrialGracePeriodDays { get; set; } = 7;

    /// <summary>Days of grace period granted when an auto-renewal payment fails. Default: 7.</summary>
    public int RenewalGracePeriodDays { get; set; } = 7;

    /// <summary>Days of grace period when a webhook payment_failed arrives. Default: 7.</summary>
    public int WebhookPaymentFailureGracePeriodDays { get; set; } = 7;

    // ── Subscription periods ───────────────────────────────────────────────────

    /// <summary>Default trial duration in days (used for subscription sync fallback). Default: 14.</summary>
    public int TrialDurationDays { get; set; } = 14;

    /// <summary>Days after period end before invoice is considered overdue. Default: 30.</summary>
    public int InvoiceDueDays { get; set; } = 30;

    // ── Renewal engine ────────────────────────────────────────────────────────

    /// <summary>Hours ahead of period end to process renewals. Default: 24.</summary>
    public int RenewalLookAheadHours { get; set; } = 24;

    // ── Access cache TTLs ─────────────────────────────────────────────────────

    /// <summary>L1 (in-process Memory) cache TTL for access snapshots. Default: 20 seconds.</summary>
    public int AccessCacheL1TtlSeconds { get; set; } = 20;

    /// <summary>L2 (Redis distributed) cache TTL for access snapshots. Default: 5 minutes.</summary>
    public int AccessCacheL2TtlMinutes { get; set; } = 5;

    /// <summary>Version key TTL in Redis. Long-lived so orphaned snapshots expire naturally. Default: 30 days.</summary>
    public int AccessCacheVersionTtlDays { get; set; } = 30;

    // ── Computed helpers ──────────────────────────────────────────────────────

    public TimeSpan L1Ttl           => TimeSpan.FromSeconds(AccessCacheL1TtlSeconds);
    public TimeSpan L2Ttl           => TimeSpan.FromMinutes(AccessCacheL2TtlMinutes);
    public TimeSpan VersionTtl      => TimeSpan.FromDays(AccessCacheVersionTtlDays);
}

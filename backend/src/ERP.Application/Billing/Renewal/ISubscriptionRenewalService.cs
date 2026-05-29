namespace ERP.Application.Billing.Renewal;

/// <summary>
/// Orchestrates the subscription renewal lifecycle:
/// 1. Detect upcoming renewals (CurrentPeriodEndUtc approaching)
/// 2. Generate draft SaasBillingInvoice for the next period
/// 3. Trigger payment attempt via IPaymentProvider
/// 4. On success: advance period, activate, emit InvoicePaid
/// 5. On failure: enter grace period via SubscriptionLifecycleOrchestrator
///
/// Idempotent: calling twice for the same period has no effect.
/// Called from Hangfire SubscriptionRenewalJob every hour.
/// </summary>
public interface ISubscriptionRenewalService
{
    /// <summary>
    /// Processes all subscribers whose billing period ends within the next <paramref name="lookAheadHours"/> hours.
    /// Generates invoices and queues payment attempts.
    /// </summary>
    Task ProcessUpcomingRenewalsAsync(int lookAheadHours = 24, CancellationToken ct = default);

    /// <summary>
    /// Forces renewal processing for a single subscriber regardless of timing.
    /// Used by admins and integration tests.
    /// </summary>
    Task ProcessSubscriberRenewalAsync(Guid subscriberId, CancellationToken ct = default);
}

/// <summary>Renewal outcome for a single subscriber.</summary>
public sealed record RenewalOutcome(
    Guid    SubscriberId,
    bool    Succeeded,
    string? FailureReason,
    string  TransitionApplied);

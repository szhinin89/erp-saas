using ERP.Domain.Billing.Enums;

namespace ERP.Application.Billing.Governance;

public sealed record BillingGovernanceState(
    Guid SubscriberId,
    BillingAccountStatus? BillingStatus,
    BillingRenewalState RenewalState,
    BillingTrialState TrialState,
    bool AllowsPlatformAccess,
    DateTime? GracePeriodEndsAtUtc,
    DateTime? CurrentPeriodEndUtc);

/// <summary>
/// Read-only state inspection service for the billing account.
/// Provides billing state queries and event recording.
///
/// IMPORTANT — Write operations (StartGracePeriod, Suspend, Reactivate) are deprecated here.
/// Use <see cref="ERP.Application.Common.Subscriptions.ISubscriptionLifecycleOrchestrator"/> instead,
/// which provides atomic transactions, proper SubscriberSubscription sync, dual-cache invalidation,
/// audit enrichment, and domain events — guarantees that BillingGovernanceService methods do NOT have.
/// </summary>
public interface IBillingGovernanceService
{
    // ── State inspection (ACTIVE — use freely) ────────────────────────────────

    Task<BillingGovernanceState> GetStateAsync(Guid subscriberId, CancellationToken ct = default);

    Task<bool> CanAccessPlatformAsync(Guid subscriberId, CancellationToken ct = default);

    // ── Provisioning (ACTIVE — provisioning only) ─────────────────────────────

    Task EnsureBillingAccountAsync(
        Guid subscriberId,
        string billingEmail,
        Guid actorId,
        CancellationToken ct = default);

    // ── Event recording (ACTIVE) ──────────────────────────────────────────────

    Task RecordEventAsync(
        Guid subscriberId,
        string eventType,
        BillingEventSource source,
        Guid actorId,
        string? payloadJson = null,
        string? correlationId = null,
        CancellationToken ct = default);

    // ── DEPRECATED: use ISubscriptionLifecycleOrchestrator ───────────────────

    /// <inheritdoc cref="ERP.Application.Common.Subscriptions.ISubscriptionLifecycleOrchestrator.EnterGracePeriodAsync"/>
    [Obsolete("Use ISubscriptionLifecycleOrchestrator.EnterGracePeriodAsync for atomic, consistent transitions.")]
    Task StartGracePeriodAsync(Guid subscriberId, int graceDays, Guid actorId, CancellationToken ct = default);

    /// <inheritdoc cref="ERP.Application.Common.Subscriptions.ISubscriptionLifecycleOrchestrator.SuspendAsync"/>
    [Obsolete("Use ISubscriptionLifecycleOrchestrator.SuspendAsync for atomic, consistent transitions.")]
    Task SuspendForNonPaymentAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);

    /// <inheritdoc cref="ERP.Application.Common.Subscriptions.ISubscriptionLifecycleOrchestrator.ActivateAsync"/>
    [Obsolete("Use ISubscriptionLifecycleOrchestrator.ActivateAsync for atomic, consistent transitions.")]
    Task ReactivateAsync(Guid subscriberId, Guid actorId, CancellationToken ct = default);
}

namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Central authority for ALL subscriber lifecycle transitions.
/// Every transition is atomic: Subscriber + BillingAccount + Subscription change together or not at all.
/// Handlers MUST delegate here instead of modifying entities directly.
/// Throws <see cref="ERP.Domain.Subscribers.Exceptions.InvalidLifecycleTransitionException"/> on disallowed transitions.
/// Throws <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> on concurrent writes.
/// </summary>
public interface ISubscriptionLifecycleOrchestrator
{
    /// <summary>Moves subscriber to Active. Clears suspension data.</summary>
    Task ActivateAsync(
        Guid subscriberId,
        Guid actorId,
        string? notes = null,
        CancellationToken ct = default);

    /// <summary>Suspends subscriber and billing account atomically.</summary>
    Task SuspendAsync(
        Guid subscriberId,
        string reason,
        Guid actorId,
        CancellationToken ct = default);

    /// <summary>Puts subscriber into Trial lifecycle and sets billing trial dates.</summary>
    Task StartTrialAsync(
        Guid subscriberId,
        DateTime endsAtUtc,
        Guid actorId,
        CancellationToken ct = default);

    /// <summary>Enters grace period on both Subscriber and BillingAccount.</summary>
    Task EnterGracePeriodAsync(
        Guid subscriberId,
        DateTime endsAtUtc,
        Guid actorId,
        CancellationToken ct = default);

    /// <summary>Cancels billing account. Subscriber moves to Suspended.</summary>
    Task CancelAsync(
        Guid subscriberId,
        string reason,
        Guid actorId,
        CancellationToken ct = default);
}

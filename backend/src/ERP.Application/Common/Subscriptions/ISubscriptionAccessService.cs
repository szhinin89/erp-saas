namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Single source of truth for whether a subscriber may operate the platform.
/// All controllers, middleware, and jobs MUST use this service for access decisions.
/// Do NOT call subscriber.IsActive, account.AllowsPlatformAccess, or
/// subscription.IsAccessible directly.
/// </summary>
public interface ISubscriptionAccessService
{
    /// <summary>
    /// Evaluates whether <paramref name="subscriberId"/> may access the platform right now.
    /// Returns <see cref="SubscriptionAccessResult.Allow()"/> for Guid.Empty (anonymous / system).
    /// </summary>
    Task<SubscriptionAccessResult> EvaluateAsync(Guid subscriberId, CancellationToken ct = default);
}

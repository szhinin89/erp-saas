namespace ERP.Application.Common.Subscriptions;

/// <summary>
/// Fast access decision cache for <see cref="ISubscriptionAccessService"/>.
/// Implementations MUST be resilient — a cache failure must never block access evaluation.
/// </summary>
public interface ISubscriptionAccessCache
{
    /// <summary>
    /// Returns the cached snapshot, or null on miss / error.
    /// NEVER throws — swallows internal failures.
    /// </summary>
    Task<SubscriptionAccessSnapshot?> TryGetAsync(Guid subscriberId, CancellationToken ct = default);

    /// <summary>
    /// Stores a snapshot. NEVER throws — swallows internal failures.
    /// </summary>
    Task SetAsync(SubscriptionAccessSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Removes any cached snapshot for this subscriber (e.g., on lifecycle transition).
    /// NEVER throws.
    /// </summary>
    Task InvalidateAsync(Guid subscriberId, CancellationToken ct = default);
}

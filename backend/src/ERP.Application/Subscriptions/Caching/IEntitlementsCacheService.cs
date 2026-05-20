namespace ERP.Application.Subscriptions.Caching;

/// <summary>
/// Fachada enterprise para cache distribuido de entitlements (Redis / IDistributedCache).
/// </summary>
public interface IEntitlementsCacheService
{
    Task<CachedSubscriberEntitlements?> GetSnapshotAsync(Guid subscriberId, CancellationToken ct = default);

    Task SetSnapshotAsync(CachedSubscriberEntitlements cached, TimeSpan? ttl = null, CancellationToken ct = default);

    Task InvalidateSnapshotAsync(Guid subscriberId, CancellationToken ct = default);

    Task<long> GetSnapshotVersionAsync(Guid subscriberId, CancellationToken ct = default);
}

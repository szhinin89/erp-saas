namespace ERP.Application.Subscriptions.Caching;

public interface ISubscriberEntitlementsSnapshotCache
{
    Task<CachedSubscriberEntitlements?> GetAsync(Guid subscriberId, CancellationToken ct = default);

    Task SetAsync(CachedSubscriberEntitlements cached, TimeSpan? ttl = null, CancellationToken ct = default);

    Task<long> GetCurrentVersionAsync(Guid subscriberId, CancellationToken ct = default);
}

public interface ISubscriberEntitlementsCacheInvalidator
{
    Task InvalidateAsync(Guid subscriberId, CancellationToken ct = default);
}

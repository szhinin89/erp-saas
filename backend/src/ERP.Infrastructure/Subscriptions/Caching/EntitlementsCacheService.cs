using ERP.Application.Subscriptions.Caching;

namespace ERP.Infrastructure.Subscriptions.Caching;

public sealed class EntitlementsCacheService : IEntitlementsCacheService
{
    private readonly ISubscriberEntitlementsSnapshotCache _inner;
    private readonly ISubscriberEntitlementsCacheInvalidator _invalidator;

    public EntitlementsCacheService(
        ISubscriberEntitlementsSnapshotCache inner,
        ISubscriberEntitlementsCacheInvalidator invalidator)
    {
        _inner = inner;
        _invalidator = invalidator;
    }

    public Task<CachedSubscriberEntitlements?> GetSnapshotAsync(Guid subscriberId, CancellationToken ct = default)
        => _inner.GetAsync(subscriberId, ct);

    public Task SetSnapshotAsync(CachedSubscriberEntitlements cached, TimeSpan? ttl = null, CancellationToken ct = default)
        => _inner.SetAsync(cached, ttl, ct);

    public Task InvalidateSnapshotAsync(Guid subscriberId, CancellationToken ct = default)
        => _invalidator.InvalidateAsync(subscriberId, ct);

    public Task<long> GetSnapshotVersionAsync(Guid subscriberId, CancellationToken ct = default)
        => _inner.GetCurrentVersionAsync(subscriberId, ct);
}

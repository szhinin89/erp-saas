namespace ERP.Application.Subscriptions.Caching;

/// <summary>Wrapper Redis-ready del snapshot de entitlements.</summary>
public sealed record CachedSubscriberEntitlements(
    SubscriberEntitlementsSnapshot Snapshot,
    long Version,
    DateTime CachedAtUtc,
    DateTime? ExpiresAtUtc)
{
    public string CacheKey(Guid subscriberId) => $"entitlements:{subscriberId:N}:v{Version}";
}

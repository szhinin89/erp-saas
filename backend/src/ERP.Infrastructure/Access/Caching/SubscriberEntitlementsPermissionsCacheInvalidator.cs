using ERP.Application.Access.Caching;
using ERP.Application.Subscriptions.Caching;
using ERP.Infrastructure.Subscriptions.Caching;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// Al invalidar entitlements de suscriptor, incrementa versión de permisos del tenant.
/// </summary>
public sealed class SubscriberEntitlementsPermissionsCacheInvalidator : ISubscriberEntitlementsCacheInvalidator
{
    private readonly DistributedSubscriberEntitlementsSnapshotCache _inner;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public SubscriberEntitlementsPermissionsCacheInvalidator(
        DistributedSubscriberEntitlementsSnapshotCache inner,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _inner = inner;
        _permissionsCache = permissionsCache;
    }

    public async Task InvalidateAsync(Guid subscriberId, CancellationToken ct = default)
    {
        await _inner.InvalidateAsync(subscriberId, ct);
        await _permissionsCache.BumpSubscriberVersionAsync(subscriberId, ct);
    }
}

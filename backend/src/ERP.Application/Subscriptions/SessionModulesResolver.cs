using ERP.Application.Common;

namespace ERP.Application.Subscriptions;

public sealed class SessionModulesResolver : ISessionModulesResolver
{
    private readonly ISubscriberEntitlementsService _entitlements;

    public SessionModulesResolver(ISubscriberEntitlementsService entitlements)
    {
        _entitlements = entitlements;
    }

    public Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(
        Guid subscriberId,
        CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        return SubscriberSubscriptionCatalog.ResolveEnabledModulesAsync(subscriberId, _entitlements, ct);
    }
}

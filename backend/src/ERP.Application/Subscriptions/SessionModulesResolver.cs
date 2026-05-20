using ERP.Application.Common;

namespace ERP.Application.Subscriptions;

public sealed class SessionModulesResolver : ISessionModulesResolver
{
    private readonly ITenantEntitlementsService _entitlements;

    public SessionModulesResolver(ITenantEntitlementsService entitlements)
    {
        _entitlements = entitlements;
    }

    public Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

        return TenantSubscriptionCatalog.ResolveEnabledModulesAsync(tenantId, _entitlements, ct);
    }
}

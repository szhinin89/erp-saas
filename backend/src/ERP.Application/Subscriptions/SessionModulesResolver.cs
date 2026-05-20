using ERP.Application.Common;
using ERP.Application.Common.Config;
using ERP.Domain.Tenants.Entities;
using Microsoft.Extensions.Options;

namespace ERP.Application.Subscriptions;

public sealed class SessionModulesResolver : ISessionModulesResolver
{
    private readonly ITenantEntitlementsService _entitlements;
    private readonly SaasEntitlementsOptions _options;

    public SessionModulesResolver(
        ITenantEntitlementsService entitlements,
        IOptions<SaasEntitlementsOptions> options)
    {
        _entitlements = entitlements;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> GetEnabledModuleKeysAsync(
        Guid tenantId,
        Tenant? legacyTenantRow = null,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Array.Empty<string>();

        if (_options.PreferLegacyEnabledModulesJsonForSession && legacyTenantRow is not null)
        {
#pragma warning disable CS0618
            return TenantSubscriptionCatalog.GetEffectiveEnabledModules(legacyTenantRow);
#pragma warning restore CS0618
        }

        if (!_options.FailClosedWithoutActiveSubscription)
        {
            // Diagnóstico: permitir lectura legacy cuando no hay filas de suscripción.
            var relational = await TenantSubscriptionCatalog.ResolveEnabledModulesAsync(
                tenantId, _entitlements, ct);
            if (relational.Count > 0 || legacyTenantRow is null)
                return relational;

#pragma warning disable CS0618
            return TenantSubscriptionCatalog.GetEffectiveEnabledModules(legacyTenantRow);
#pragma warning restore CS0618
        }

        return await TenantSubscriptionCatalog.ResolveEnabledModulesAsync(tenantId, _entitlements, ct);
    }
}

using ERP.Application.Subscriptions;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Subscriptions;

namespace ERP.API.Tests.Support;

/// <summary>Entitlements ilimitados para integración: evita fail-closed sin suscripción activa.</summary>
internal sealed class AllowAllEntitlementsService : ISubscriberEntitlementsService
{
    private static readonly SubscriberEntitlementsSnapshot OpenSnapshot = new(
        PlanCode: "TEST-UNLIMITED",
        PlanName: "Test Unlimited",
        EnabledModules: Array.Empty<string>(),
        EnabledFeatures: Array.Empty<string>(),
        Limits: new Dictionary<string, int?>(),
        HasModuleRestrictions: false,
        CommercialLimits: new Dictionary<string, EffectiveCommercialLimit>());

    public Task<IReadOnlyCollection<string>> GetEnabledModuleKeysAsync(Guid subscriberId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyCollection<string>>(Array.Empty<string>());

    public Task<bool> HasFeatureAsync(Guid subscriberId, string featureCode, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<int?> GetLimitPerPeriodAsync(Guid subscriberId, string featureCode, CancellationToken ct = default)
        => Task.FromResult<int?>(null);

    public Task<bool> AllowsPermissionAsync(Guid subscriberId, string permissionKey, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<SubscriberEntitlementsSnapshot> GetEntitlementsSnapshotAsync(Guid subscriberId, CancellationToken ct = default)
        => Task.FromResult(OpenSnapshot with { SubscriberId = subscriberId == Guid.Empty ? null : subscriberId });
}

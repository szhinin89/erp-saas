namespace ERP.Application.Subscriptions;

using ERP.Application.Subscriptions.CommercialPlanLimits;

/// <summary>Snapshot de entitlements comerciales del tenant (única lectura para UI/API). Redis-ready.</summary>
public sealed record SubscriberEntitlementsSnapshot(
    string? PlanCode,
    string? PlanName,
    IReadOnlyList<string> EnabledModules,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyDictionary<string, int?> Limits,
    bool HasModuleRestrictions,
    IReadOnlyDictionary<string, EffectiveCommercialLimit>? CommercialLimits = null,
    Guid? SubscriberId = null,
    string? BillingAccountStatus = null,
    long Version = 0,
    DateTime? ResolvedAtUtc = null);

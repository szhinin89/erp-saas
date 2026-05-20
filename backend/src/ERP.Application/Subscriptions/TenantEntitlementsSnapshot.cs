namespace ERP.Application.Subscriptions;

/// <summary>Snapshot de entitlements comerciales del tenant (única lectura para UI/API).</summary>
public sealed record TenantEntitlementsSnapshot(
    string? PlanCode,
    string? PlanName,
    IReadOnlyList<string> EnabledModules,
    IReadOnlyList<string> EnabledFeatures,
    IReadOnlyDictionary<string, int?> Limits,
    bool HasModuleRestrictions);

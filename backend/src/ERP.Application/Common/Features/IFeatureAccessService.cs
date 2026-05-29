namespace ERP.Application.Common.Features;

/// <summary>
/// Feature and quota governance — completely separate from subscription lifecycle.
///
/// SEPARATION OF CONCERNS:
///   SubscriptionLifecycleOrchestrator → controls platform access (Active/Suspended/etc.)
///   ISubscriptionAccessService        → evaluates access gate (Blocked/ReadOnly/FullAccess)
///   IFeatureAccessService             → evaluates feature and quota entitlements
///
/// Features are plan-based (e.g., "modules.ventas", "features.electronic_billing").
/// Quotas are measured limits (e.g., max_companies=5, max_users=10).
///
/// This service is NOT responsible for lifecycle. A suspended subscriber can still have
/// feature config evaluated (e.g., for admin overrides), though access is blocked upstream.
/// </summary>
public interface IFeatureAccessService
{
    // ── Feature gates ─────────────────────────────────────────────────────────

    /// <summary>Returns true if the subscriber's plan includes this feature key.</summary>
    Task<bool> HasFeatureAsync(Guid subscriberId, string featureKey, CancellationToken ct = default);

    /// <summary>Returns all enabled module keys for this subscriber's plan.</summary>
    Task<IReadOnlyList<string>> GetEnabledModulesAsync(Guid subscriberId, CancellationToken ct = default);

    // ── Quota enforcement ─────────────────────────────────────────────────────

    /// <summary>Returns the effective limit for a quota code (e.g., "max_companies").</summary>
    Task<int?> GetQuotaLimitAsync(Guid subscriberId, string quotaCode, CancellationToken ct = default);

    /// <summary>Returns the current usage for a quota code.</summary>
    Task<int> GetQuotaUsageAsync(Guid subscriberId, string quotaCode, CancellationToken ct = default);

    /// <summary>
    /// True if the subscriber can create one more of this resource.
    /// (usage + increment) <= limit
    /// </summary>
    Task<bool> CanIncrementQuotaAsync(
        Guid subscriberId, string quotaCode, int increment = 1, CancellationToken ct = default);

    // ── Snapshot ──────────────────────────────────────────────────────────────

    /// <summary>Returns a complete snapshot of features + quotas for this subscriber.</summary>
    Task<FeatureAccessSnapshot> GetSnapshotAsync(Guid subscriberId, CancellationToken ct = default);
}

/// <summary>Snapshot of all feature/quota entitlements for a subscriber.</summary>
public sealed class FeatureAccessSnapshot
{
    public Guid                           SubscriberId   { get; init; }
    public string?                        PlanCode       { get; init; }
    public IReadOnlyList<string>          EnabledModules { get; init; } = [];
    public IReadOnlyList<string>          EnabledFeatures { get; init; } = [];
    public IReadOnlyDictionary<string,int> QuotaLimits   { get; init; } = new Dictionary<string,int>();
    public IReadOnlyDictionary<string,int> QuotaUsages   { get; init; } = new Dictionary<string,int>();
    public DateTime                       ResolvedAtUtc  { get; init; }
}

/// <summary>Well-known quota codes — avoid magic strings.</summary>
public static class QuotaCodes
{
    public const string MaxCompanies   = "max_companies";
    public const string MaxUsers       = "max_users";
    public const string MaxBranches    = "max_branches";
    public const string MaxWarehouses  = "max_warehouses";
    public const string MaxPosTerminals = "max_pos_terminals";
    public const string ApiCallsPerMonth = "api_calls_per_month";
}

/// <summary>Well-known feature keys.</summary>
public static class FeatureKeys
{
    public const string ElectronicBilling = "features.electronic_billing";
    public const string MultiCompany      = "features.multi_company";
    public const string AdvancedReports   = "features.advanced_reports";
    public const string ApiAccess         = "features.api_access";
    public const string Webhooks          = "features.webhooks";
}

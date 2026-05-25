using ERP.Application.Billing.Governance;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscriptions.Caching;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Resuelve entitlements solo desde suscripción activa, plan, features y overrides.
/// </summary>
public sealed class SubscriberEntitlementsService : ISubscriberEntitlementsService
{
    private readonly ErpDbContext _db;
    private readonly IPlatformQueryAccessor _platform;
    private readonly ICommercialPlanLimitService _planLimits;
    private readonly ISubscriberEntitlementsSnapshotCache? _cache;
    private readonly IBillingGovernanceService _billingGovernance;
    private readonly SubscriberEntitlementsCacheOptions _cacheOptions;

    public SubscriberEntitlementsService(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        ICommercialPlanLimitService planLimits,
        IBillingGovernanceService billingGovernance,
        IOptions<SubscriberEntitlementsCacheOptions> cacheOptions,
        ISubscriberEntitlementsSnapshotCache? cache = null)
    {
        _db = db;
        _platform = platform;
        _planLimits = planLimits;
        _billingGovernance = billingGovernance;
        _cacheOptions = cacheOptions.Value;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<string>> GetEnabledModuleKeysAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return Array.Empty<string>();

        var snapshot = await LoadEntitlementSnapshotAsync(subscriberId, ct);
        if (snapshot is null)
            return Array.Empty<string>();

        return snapshot.Features
            .Where(f => f.Kind == PlatformFeatureKind.Module && f.IsEntitled)
            .Select(f => NormalizeModuleKey(f.ResourceRef, f.Code))
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<bool> HasFeatureAsync(Guid subscriberId, string featureCode, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return true;

        var code = NormalizeFeatureCode(featureCode);
        if (code.Length == 0)
            return false;

        var snapshot = await LoadEntitlementSnapshotAsync(subscriberId, ct);
        if (snapshot is null)
            return false;

        return snapshot.Features
            .FirstOrDefault(f => string.Equals(f.Code, code, StringComparison.Ordinal))
            ?.IsEntitled ?? false;
    }

    public Task<bool> AllowsPermissionAsync(Guid subscriberId, string permissionKey, CancellationToken ct = default) =>
        SubscriberSubscriptionCatalog.SubscriberAllowsPermissionAsync(subscriberId, this, permissionKey, ct);

    public async Task<SubscriberEntitlementsSnapshot> GetEntitlementsSnapshotAsync(
        Guid subscriberId,
        CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
        {
            return new SubscriberEntitlementsSnapshot(
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                new Dictionary<string, int?>(StringComparer.Ordinal),
                HasModuleRestrictions: true);
        }

        if (_cache is not null && _cacheOptions.Enabled)
        {
            var cached = await _cache.GetAsync(subscriberId, ct);
            if (cached is not null)
                return cached.Snapshot;
        }

        var built = await BuildEntitlementsSnapshotAsync(subscriberId, ct);

        if (_cache is not null && _cacheOptions.Enabled)
        {
            var version = await _cache.GetCurrentVersionAsync(subscriberId, ct);
            if (version <= 0)
                version = 1;
            await _cache.SetAsync(
                new CachedSubscriberEntitlements(built, version, DateTime.UtcNow, null),
                TimeSpan.FromSeconds(_cacheOptions.TtlSeconds),
                ct);
        }

        return built;
    }

    private async Task<SubscriberEntitlementsSnapshot> BuildEntitlementsSnapshotAsync(
        Guid subscriberId,
        CancellationToken ct)
    {
        var snapshot = await LoadEntitlementSnapshotAsync(subscriberId, ct);
        if (snapshot is null)
        {
            return new SubscriberEntitlementsSnapshot(
                null,
                null,
                Array.Empty<string>(),
                Array.Empty<string>(),
                new Dictionary<string, int?>(StringComparer.Ordinal),
                HasModuleRestrictions: true);
        }

        var plan = await _db.CommercialPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == snapshot.PlanId, ct);

        var modules = snapshot.Features
            .Where(f => f.Kind == PlatformFeatureKind.Module && f.IsEntitled)
            .Select(f => SubscriberSubscriptionCatalog.NormalizeModuleKey(
                NormalizeModuleKey(f.ResourceRef, f.Code)))
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        var features = snapshot.Features
            .Where(f => f.IsEntitled && f.Kind != PlatformFeatureKind.Module)
            .Select(f => f.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var limits = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in snapshot.Features.Where(f => f.IsEntitled && f.IsMetered))
        {
            var code = NormalizeFeatureCode(f.Code);
            if (code.Length == 0)
                continue;
            limits[code] = ToIntLimit(f.EffectiveLimitPerPeriod);
        }

        var hasRestrictions = SubscriberSubscriptionCatalog.HasModuleRestrictionsFromModules(modules);

        var commercialLimitsSnapshot = await _planLimits.GetEffectiveLimitsSnapshotAsync(subscriberId, ct);
        var billingState = await _billingGovernance.GetStateAsync(subscriberId, ct);
        var version = _cache is not null
            ? await _cache.GetCurrentVersionAsync(subscriberId, ct)
            : 0L;
        if (version <= 0)
            version = 1;

        return new SubscriberEntitlementsSnapshot(
            plan?.Code,
            plan?.Name,
            modules,
            features,
            limits,
            hasRestrictions,
            commercialLimitsSnapshot.LimitsByCode,
            subscriberId,
            billingState.BillingStatus?.ToString(),
            version,
            DateTime.UtcNow);
    }

    public async Task<int?> GetLimitPerPeriodAsync(Guid subscriberId, string featureCode, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return null;

        var code = NormalizeFeatureCode(featureCode);
        if (code.Length == 0)
            return null;

        var snapshot = await LoadEntitlementSnapshotAsync(subscriberId, ct);
        if (snapshot is null)
            return null;

        var feature = snapshot.Features
            .FirstOrDefault(f => string.Equals(f.Code, code, StringComparison.Ordinal));
        if (feature is null || !feature.IsEntitled || !feature.IsMetered)
            return null;

        return ToIntLimit(feature.EffectiveLimitPerPeriod);
    }

    private async Task<EntitlementSnapshot?> LoadEntitlementSnapshotAsync(Guid subscriberId, CancellationToken ct)
    {
        var subscription = await _platform
            .Unfiltered(_db.SubscriberSubscriptions.AsNoTracking(), PlatformQueryReason.SubscriberScopedExplicit)
            .Where(s => s.SubscriberId == subscriberId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return null;

        var planFeatures = await _db.CommercialPlanFeatures.AsNoTracking()
            .Where(pf => pf.PlanId == subscription.PlanId)
            .ToListAsync(ct);

        var featureIds = planFeatures.Select(pf => pf.FeatureId).ToList();

        var overrides = await _platform
            .Unfiltered(_db.SubscriptionFeatureOverrides.AsNoTracking(), PlatformQueryReason.SubscriberScopedExplicit)
            .Where(o => o.SubscriberId == subscriberId && o.SubscriptionId == subscription.Id)
            .ToListAsync(ct);

        featureIds.AddRange(overrides.Select(o => o.FeatureId));
        featureIds = featureIds.Distinct().ToList();

        if (featureIds.Count == 0)
            return new EntitlementSnapshot(subscription.Id, subscription.PlanId, Array.Empty<EntitledFeature>());

        var definitions = await _db.PlatformFeatures.AsNoTracking()
            .Where(f => featureIds.Contains(f.Id))
            .ToListAsync(ct);

        var planByFeature = planFeatures.ToDictionary(pf => pf.FeatureId);
        var overrideByFeature = overrides.ToDictionary(o => o.FeatureId);

        var entitled = new List<EntitledFeature>(definitions.Count);
        foreach (var def in definitions)
        {
            planByFeature.TryGetValue(def.Id, out var planRow);
            overrideByFeature.TryGetValue(def.Id, out var overrideRow);

            var isEntitled = ResolveIsEntitled(planRow, overrideRow);
            long? limit = null;
            if (isEntitled)
                limit = overrideRow?.LimitOverridePerPeriod ?? planRow?.LimitPerPeriod;

            entitled.Add(new EntitledFeature(
                def.Id,
                def.Code,
                def.Kind,
                def.IsMetered,
                def.ResourceRef,
                isEntitled,
                limit));
        }

        return new EntitlementSnapshot(subscription.Id, subscription.PlanId, entitled);
    }

    private static bool ResolveIsEntitled(CommercialPlanFeature? planRow, SubscriptionFeatureOverride? overrideRow)
    {
        if (overrideRow is { IsEnabled: false })
            return false;
        if (overrideRow is { IsEnabled: true })
            return true;
        return planRow is { IsIncluded: true };
    }

    private static string NormalizeFeatureCode(string featureCode) =>
        (featureCode ?? string.Empty).Trim().ToUpperInvariant();

    private static string NormalizeModuleKey(string? resourceRef, string code)
    {
        if (!string.IsNullOrWhiteSpace(resourceRef))
            return resourceRef.Trim().ToLowerInvariant();
        return (code ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static int? ToIntLimit(long? limit)
    {
        if (limit is null)
            return null;
        if (limit.Value > int.MaxValue)
            return int.MaxValue;
        return (int)limit.Value;
    }

    private sealed record EntitlementSnapshot(Guid SubscriptionId, Guid PlanId, IReadOnlyList<EntitledFeature> Features);

    private sealed record EntitledFeature(
        Guid FeatureId,
        string Code,
        PlatformFeatureKind Kind,
        bool IsMetered,
        string? ResourceRef,
        bool IsEntitled,
        long? EffectiveLimitPerPeriod);
}

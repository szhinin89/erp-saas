using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

/// <summary>
/// Resuelve entitlements solo desde suscripción activa, plan, features y overrides.
/// </summary>
public sealed class TenantEntitlementsService : ITenantEntitlementsService
{
    private readonly ErpDbContext _db;

    public TenantEntitlementsService(ErpDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<string>> GetEnabledModuleKeysAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return Array.Empty<string>();

        var snapshot = await LoadEntitlementSnapshotAsync(tenantId, ct);
        if (snapshot is null)
            return Array.Empty<string>();

        return snapshot.Features
            .Where(f => f.Kind == SaasFeatureKind.Module && f.IsEntitled)
            .Select(f => NormalizeModuleKey(f.ResourceRef, f.Code))
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return true;

        var code = NormalizeFeatureCode(featureCode);
        if (code.Length == 0)
            return false;

        var snapshot = await LoadEntitlementSnapshotAsync(tenantId, ct);
        if (snapshot is null)
            return false;

        return snapshot.Features
            .FirstOrDefault(f => string.Equals(f.Code, code, StringComparison.Ordinal))
            ?.IsEntitled ?? false;
    }

    public async Task<int?> GetLimitPerPeriodAsync(Guid tenantId, string featureCode, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return null;

        var code = NormalizeFeatureCode(featureCode);
        if (code.Length == 0)
            return null;

        var snapshot = await LoadEntitlementSnapshotAsync(tenantId, ct);
        if (snapshot is null)
            return null;

        var feature = snapshot.Features
            .FirstOrDefault(f => string.Equals(f.Code, code, StringComparison.Ordinal));
        if (feature is null || !feature.IsEntitled || !feature.IsMetered)
            return null;

        return ToIntLimit(feature.EffectiveLimitPerPeriod);
    }

    private async Task<EntitlementSnapshot?> LoadEntitlementSnapshotAsync(Guid tenantId, CancellationToken ct)
    {
        var subscription = await _db.TenantSaasSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == TenantSubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return null;

        var planFeatures = await _db.SaasPlanFeatures.AsNoTracking()
            .Where(pf => pf.PlanId == subscription.PlanId)
            .ToListAsync(ct);

        var featureIds = planFeatures.Select(pf => pf.FeatureId).ToList();

        var overrides = await _db.TenantSubscriptionFeatureOverrides.AsNoTracking()
            .Where(o => o.SubscriptionId == subscription.Id)
            .ToListAsync(ct);

        featureIds.AddRange(overrides.Select(o => o.FeatureId));
        featureIds = featureIds.Distinct().ToList();

        if (featureIds.Count == 0)
            return new EntitlementSnapshot(subscription.Id, subscription.PlanId, Array.Empty<EntitledFeature>());

        var definitions = await _db.SaasFeatureDefinitions.AsNoTracking()
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

    private static bool ResolveIsEntitled(SaasPlanFeature? planRow, TenantSubscriptionFeatureOverride? overrideRow)
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
        SaasFeatureKind Kind,
        bool IsMetered,
        string? ResourceRef,
        bool IsEntitled,
        long? EffectiveLimitPerPeriod);
}

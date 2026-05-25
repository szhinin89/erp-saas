using System.Text.Json;
using ERP.Application.Access.Caching;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class SubscriptionFeatureOverridesService : ISubscriptionFeatureOverridesService
{
    private readonly ErpDbContext _db;
    private readonly IPlatformQueryAccessor _platform;
    private readonly IPermissionsCacheInvalidator _permissionsCache;

    public SubscriptionFeatureOverridesService(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        IPermissionsCacheInvalidator permissionsCache)
    {
        _db = db;
        _platform = platform;
        _permissionsCache = permissionsCache;
    }

    public async Task ApplyModuleOverridesAsync(
        Guid subscriberId,
        IReadOnlyList<string>? requestedModuleKeys,
        Guid actorId,
        CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty)
            return;

        var subscription = await _platform
            .Unfiltered(_db.SubscriberSubscriptions, PlatformQueryReason.SubscriberScopedExplicit)
            .Where(s => s.SubscriberId == subscriberId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return;

        var planModuleRows = await (
            from pf in _db.CommercialPlanFeatures.AsNoTracking()
            join fd in _db.PlatformFeatures.AsNoTracking() on pf.FeatureId equals fd.Id
            where pf.PlanId == subscription.PlanId && fd.Kind == PlatformFeatureKind.Module
            select new { fd.Id, fd.ResourceRef, fd.Code, pf.IsIncluded }
        ).ToListAsync(ct);

        if (planModuleRows.Count == 0)
            return;

        var moduleFeatureIds = planModuleRows.Select(r => r.Id).ToHashSet();

        var existing = await _platform
            .Unfiltered(_db.SubscriptionFeatureOverrides, PlatformQueryReason.SubscriberScopedExplicit)
            .Where(o => o.SubscriberId == subscriberId && o.SubscriptionId == subscription.Id)
            .ToListAsync(ct);

        var moduleOverrides = existing.Where(o => moduleFeatureIds.Contains(o.FeatureId)).ToList();

        if (requestedModuleKeys is null || requestedModuleKeys.Count == 0)
        {
            if (moduleOverrides.Count > 0)
            {
                _db.SubscriptionFeatureOverrides.RemoveRange(moduleOverrides);
                await _db.SubscriberSubscriptionEvents.AddAsync(
                    SubscriberSubscriptionEvent.Create(
                        subscriberId,
                        SubscriberSubscriptionEvent.Types.ModuleOverridesCleared,
                        actorId,
                        subscriptionId: subscription.Id),
                    ct);
                await _permissionsCache.BumpSubscriberVersionAsync(subscriberId, ct);
            }

            return;
        }

        var allowed = new HashSet<string>(
            SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(requestedModuleKeys),
            StringComparer.OrdinalIgnoreCase);

        foreach (var row in planModuleRows)
        {
            var moduleKey = ResolveModuleKey(row.ResourceRef, row.Code);
            var shouldEnable = allowed.Contains(moduleKey);
            var matchesPlanDefault = shouldEnable == row.IsIncluded;
            var existingOv = moduleOverrides.FirstOrDefault(o => o.FeatureId == row.Id);

            if (matchesPlanDefault)
            {
                if (existingOv is not null)
                    _db.SubscriptionFeatureOverrides.Remove(existingOv);
                continue;
            }

            if (existingOv is null)
            {
                await _db.SubscriptionFeatureOverrides.AddAsync(
                    SubscriptionFeatureOverride.Create(
                        subscriberId,
                        subscription.Id,
                        row.Id,
                        shouldEnable,
                        limitOverridePerPeriod: null,
                        actorId),
                    ct);
            }
            else
            {
                existingOv.SetEnabled(shouldEnable, actorId);
            }
        }

        var metadata = JsonSerializer.Serialize(new { modules = allowed.OrderBy(x => x).ToArray() });
        await _db.SubscriberSubscriptionEvents.AddAsync(
            SubscriberSubscriptionEvent.Create(
                subscriberId,
                SubscriberSubscriptionEvent.Types.ModuleOverridesApplied,
                actorId,
                subscriptionId: subscription.Id,
                metadataJson: metadata),
            ct);

        await _permissionsCache.BumpSubscriberVersionAsync(subscriberId, ct);
    }

    private static string ResolveModuleKey(string? resourceRef, string code)
    {
        if (!string.IsNullOrWhiteSpace(resourceRef))
            return SubscriberSubscriptionCatalog.NormalizeModuleKey(resourceRef);
        return SubscriberSubscriptionCatalog.NormalizeModuleKey(code);
    }
}

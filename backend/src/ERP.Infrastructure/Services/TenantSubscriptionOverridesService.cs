using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class TenantSubscriptionOverridesService : ITenantSubscriptionOverridesService
{
    private readonly ErpDbContext _db;
    private readonly IPlatformQueryAccessor _platform;

    public TenantSubscriptionOverridesService(ErpDbContext db, IPlatformQueryAccessor platform)
    {
        _db = db;
        _platform = platform;
    }

    public async Task ApplyModuleOverridesAsync(
        Guid tenantId,
        IReadOnlyList<string>? requestedModuleKeys,
        Guid actorId,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            return;

        var subscription = await _platform
            .Unfiltered(_db.TenantSaasSubscriptions, PlatformQueryReason.TenantScopedExplicit)
            .Where(s => s.TenantId == tenantId && s.Status == TenantSubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (subscription is null)
            return;

        var planModuleRows = await (
            from pf in _db.SaasPlanFeatures.AsNoTracking()
            join fd in _db.SaasFeatureDefinitions.AsNoTracking() on pf.FeatureId equals fd.Id
            where pf.PlanId == subscription.PlanId && fd.Kind == SaasFeatureKind.Module
            select new { fd.Id, fd.ResourceRef, fd.Code, pf.IsIncluded }
        ).ToListAsync(ct);

        if (planModuleRows.Count == 0)
            return;

        var moduleFeatureIds = planModuleRows.Select(r => r.Id).ToHashSet();

        var existing = await _platform
            .Unfiltered(_db.TenantSubscriptionFeatureOverrides, PlatformQueryReason.TenantScopedExplicit)
            .Where(o => o.TenantId == tenantId && o.SubscriptionId == subscription.Id)
            .ToListAsync(ct);

        var moduleOverrides = existing.Where(o => moduleFeatureIds.Contains(o.FeatureId)).ToList();

        if (requestedModuleKeys is null || requestedModuleKeys.Count == 0)
        {
            if (moduleOverrides.Count > 0)
                _db.TenantSubscriptionFeatureOverrides.RemoveRange(moduleOverrides);
            return;
        }

        var allowed = new HashSet<string>(
            TenantSubscriptionCatalog.NormalizeModuleKeysInput(requestedModuleKeys),
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
                    _db.TenantSubscriptionFeatureOverrides.Remove(existingOv);
                continue;
            }

            if (existingOv is null)
            {
                await _db.TenantSubscriptionFeatureOverrides.AddAsync(
                    TenantSubscriptionFeatureOverride.Create(
                        tenantId,
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
    }

    private static string ResolveModuleKey(string? resourceRef, string code)
    {
        if (!string.IsNullOrWhiteSpace(resourceRef))
            return TenantSubscriptionCatalog.NormalizeModuleKey(resourceRef);
        return TenantSubscriptionCatalog.NormalizeModuleKey(code);
    }
}

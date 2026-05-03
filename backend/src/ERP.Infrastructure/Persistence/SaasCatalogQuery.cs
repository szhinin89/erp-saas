using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class SaasCatalogQuery : ISaasCatalogQuery, ISaasPublicPlansQuery
{
    private readonly ErpDbContext _db;

    public SaasCatalogQuery(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SaasPlanCatalogItem>> GetPlansWithFeaturesAsync(CancellationToken ct = default)
    {
        var plans = await _db.SaasPlans.AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        if (plans.Count == 0)
            return Array.Empty<SaasPlanCatalogItem>();

        var planIds = plans.Select(p => p.Id).ToList();
        var links = await _db.SaasPlanFeatures.AsNoTracking()
            .Where(pf => planIds.Contains(pf.PlanId))
            .ToListAsync(ct);

        var featureIds = links.Select(l => l.FeatureId).Distinct().ToList();
        var features = await _db.SaasFeatureDefinitions.AsNoTracking()
            .Where(f => featureIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f, ct);

        var result = new List<SaasPlanCatalogItem>(plans.Count);
        foreach (var plan in plans)
        {
            var items = links
                .Where(l => l.PlanId == plan.Id && features.ContainsKey(l.FeatureId))
                .Select(l =>
                {
                    var def = features[l.FeatureId];
                    return new SaasPlanFeatureCatalogItem(
                        def.Code,
                        def.Name,
                        def.Description,
                        def.IsMetered,
                        FeatureKindToApi(def.Kind),
                        def.ResourceRef,
                        l.IsIncluded,
                        l.LimitPerPeriod);
                })
                .OrderBy(x => x.FeatureCode)
                .ToList();

            result.Add(new SaasPlanCatalogItem(
                plan.Id,
                plan.Code,
                plan.Name,
                plan.ShortLabel,
                plan.IsActive,
                plan.PriceAmount,
                plan.Currency,
                plan.BillingCycle,
                plan.IsPubliclyVisible,
                plan.IsRecommended,
                plan.SortOrder,
                plan.ExternalBillingRef,
                items));
        }

        return result;
    }

    public async Task<IReadOnlyList<SaasPublicPlanDto>> GetVisiblePlansAsync(CancellationToken ct = default)
    {
        var plans = await _db.SaasPlans.AsNoTracking()
            .Where(p => p.IsActive && p.IsPubliclyVisible)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);

        if (plans.Count == 0)
            return Array.Empty<SaasPublicPlanDto>();

        var planIds = plans.Select(p => p.Id).ToList();
        var links = await _db.SaasPlanFeatures.AsNoTracking()
            .Where(pf => planIds.Contains(pf.PlanId) && pf.IsIncluded)
            .ToListAsync(ct);

        var featureIds = links.Select(l => l.FeatureId).Distinct().ToList();
        var defs = await _db.SaasFeatureDefinitions.AsNoTracking()
            .Where(f => featureIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f, ct);

        var list = new List<SaasPublicPlanDto>(plans.Count);
        foreach (var plan in plans)
        {
            var feats = links
                .Where(l => l.PlanId == plan.Id && defs.ContainsKey(l.FeatureId))
                .Select(l =>
                {
                    var d = defs[l.FeatureId];
                    return new SaasPublicPlanFeatureDto(
                        d.Code,
                        d.Name,
                        d.Description,
                        d.IsMetered,
                        FeatureKindToApi(d.Kind),
                        d.ResourceRef,
                        l.IsIncluded,
                        l.LimitPerPeriod);
                })
                .OrderBy(x => x.Code)
                .ToList();

            list.Add(new SaasPublicPlanDto(
                plan.Id,
                plan.Code,
                plan.Name,
                plan.ShortLabel,
                plan.PriceAmount,
                plan.Currency,
                plan.BillingCycle,
                plan.IsRecommended,
                plan.SortOrder,
                feats));
        }

        return list;
    }

    private static string FeatureKindToApi(SaasFeatureKind kind) =>
        kind.ToString().ToLowerInvariant();
}

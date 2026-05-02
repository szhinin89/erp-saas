using ERP.Application.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

public sealed class SaasCatalogQuery : ISaasCatalogQuery
{
    private readonly ErpDbContext _db;

    public SaasCatalogQuery(ErpDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SaasPlanCatalogItem>> GetPlansWithFeaturesAsync(CancellationToken ct = default)
    {
        var plans = await _db.SaasPlans.AsNoTracking()
            .OrderBy(p => p.Code)
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
                .Where(l => l.PlanId == plan.Id)
                .Select(l =>
                {
                    var def = features[l.FeatureId];
                    return new SaasPlanFeatureCatalogItem(
                        def.Code,
                        def.Name,
                        def.Description,
                        def.IsMetered,
                        l.IsIncluded,
                        l.LimitPerPeriod);
                })
                .OrderBy(x => x.FeatureCode)
                .ToList();

            result.Add(new SaasPlanCatalogItem(plan.Id, plan.Code, plan.Name, plan.IsActive, items));
        }

        return result;
    }
}

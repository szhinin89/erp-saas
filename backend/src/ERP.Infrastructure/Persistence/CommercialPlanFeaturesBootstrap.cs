using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Semillas idempotentes de <c>platform_features</c> y <c>commercial_plan_features</c>.
/// Alinea módulos del plan con el menú comercial (p. ej. logística en Starter).
/// </summary>
public static class CommercialPlanFeaturesBootstrap
{
    private sealed record ModuleSeed(string Code, string ResourceRef);

    private static readonly ModuleSeed[] StarterModules =
    [
        new("ACCESS", "access"),
        new("ACCOUNTING", "accounting"),
        new("EXPENSES", "expenses"),
        new("INVENTORY", "inventory"),
        new("PURCHASES", "purchases"),
        new("SALES", "sales"),
        new("LOGISTICS", "logistics"),
    ];

    private static readonly ModuleSeed[] FullErpModules =
    [
        .. StarterModules,
        new("PAYROLL", "payroll"),
    ];

    private static readonly IReadOnlyDictionary<string, ModuleSeed[]> ModulesByPlan =
        new Dictionary<string, ModuleSeed[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["starter"] = StarterModules,
            ["business"] = FullErpModules,
            ["professional"] = FullErpModules,
            ["enterprise"] = FullErpModules,
        };

    public static async Task EnsureDefaultsAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var plans = await db.CommercialPlans.AsNoTracking().ToListAsync(ct);
        if (plans.Count == 0)
            return;

        var planByCode = plans
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .ToDictionary(p => p.Code!.Trim().ToLowerInvariant(), p => p.Id);

        var changed = false;

        foreach (var (planCode, modules) in ModulesByPlan)
        {
            if (!planByCode.TryGetValue(planCode, out var planId))
                continue;

            foreach (var module in modules)
            {
                var feature = await db.PlatformFeatures
                    .FirstOrDefaultAsync(f => f.Code == module.Code, ct);
                if (feature is null)
                {
                    feature = PlatformFeature.Create(
                        module.Code,
                        module.Code,
                        null,
                        isMetered: false,
                        PlatformFeatureKind.Module,
                        module.ResourceRef);
                    db.PlatformFeatures.Add(feature);
                    await db.SaveChangesAsync(ct);
                }

                var linked = await db.CommercialPlanFeatures
                    .AnyAsync(pf => pf.PlanId == planId && pf.FeatureId == feature.Id, ct);
                if (!linked)
                {
                    db.CommercialPlanFeatures.Add(
                        CommercialPlanFeature.Create(planId, feature.Id, isIncluded: true, limitPerPeriod: null));
                    changed = true;
                }
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

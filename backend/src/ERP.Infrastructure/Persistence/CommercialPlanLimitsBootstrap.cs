using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Semillas idempotentes de <c>commercial_plan_limits</c> (Fase 4).
/// </summary>
public static class CommercialPlanLimitsBootstrap
{
    private sealed record LimitSeed(string PlanCode, string LimitCode, long LimitValue);

    private static readonly LimitSeed[] DefaultLimits =
    [
        new("starter", CommercialPlanLimit.Codes.MaxCompanies, 1),
        new("starter", CommercialPlanLimit.Codes.MaxUsers, 5),
        new("starter", CommercialPlanLimit.Codes.MaxBranches, 2),
        new("starter", CommercialPlanLimit.Codes.MaxWarehouses, 2),
        new("business", CommercialPlanLimit.Codes.MaxCompanies, 3),
        new("business", CommercialPlanLimit.Codes.MaxUsers, 25),
        new("business", CommercialPlanLimit.Codes.MaxBranches, 10),
        new("business", CommercialPlanLimit.Codes.MaxWarehouses, 10),
        new("professional", CommercialPlanLimit.Codes.MaxCompanies, 10),
        new("professional", CommercialPlanLimit.Codes.MaxUsers, 100),
        new("professional", CommercialPlanLimit.Codes.MaxBranches, 50),
        new("professional", CommercialPlanLimit.Codes.MaxWarehouses, 50),
        new("enterprise", CommercialPlanLimit.Codes.MaxCompanies, 0),
        new("enterprise", CommercialPlanLimit.Codes.MaxUsers, 0),
        new("enterprise", CommercialPlanLimit.Codes.MaxBranches, 0),
        new("enterprise", CommercialPlanLimit.Codes.MaxWarehouses, 0),
    ];

    public static async Task EnsureDefaultsAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var plans = await db.CommercialPlans.AsNoTracking().ToListAsync(ct);
        if (plans.Count == 0)
            return;

        var planByCode = plans
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .ToDictionary(p => p.Code!.Trim().ToLowerInvariant(), p => p.Id);

        var existing = await db.CommercialPlanLimits.ToListAsync(ct);
        var changed = false;

        foreach (var seed in DefaultLimits)
        {
            var code = seed.PlanCode.Trim().ToLowerInvariant();
            if (!planByCode.TryGetValue(code, out var planId))
                continue;

            var limitCode = seed.LimitCode.Trim().ToUpperInvariant();
            var row = existing.FirstOrDefault(
                x => x.CommercialPlanId == planId &&
                     string.Equals(x.LimitCode, limitCode, StringComparison.OrdinalIgnoreCase));

            if (row is null)
            {
                db.CommercialPlanLimits.Add(
                    CommercialPlanLimit.Create(planId, limitCode, seed.LimitValue));
                changed = true;
            }
        }

        if (changed)
            await db.SaveChangesAsync(ct);
    }
}

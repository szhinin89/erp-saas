using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence;

/// <summary>
/// Inserta catálogo mínimo de planes comerciales cuando la BD no tiene planes.
/// Idempotente: no duplica ni sobreescribe planes existentes.
/// </summary>
public static class SaasPlansBootstrap
{
    private sealed record DefaultPlanSeed(
        string Code,
        string Name,
        string ShortLabel,
        decimal PriceAmount,
        bool IsRecommended,
        int SortOrder);

    private static readonly DefaultPlanSeed[] DefaultPlans =
    [
        new("starter", "Starter", "STARTER", 49m,  false, 0),
        new("business", "Business", "BUSINESS", 99m,  true,  1),
        new("professional", "Professional", "PROFESSIONAL", 179m, false, 2),
        new("enterprise", "Enterprise", "ENTERPRISE", 299m, false, 3),
    ];

    public static async Task EnsureDefaultsAsync(ErpDbContext db, CancellationToken ct = default)
    {
        var existingCodes = await db.SaasPlans.AsNoTracking()
            .Select(p => p.Code)
            .ToListAsync(ct);

        var existing = existingCodes
            .Select(c => (c ?? string.Empty).Trim().ToLowerInvariant())
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var toInsert = new List<SaasPlan>();
        foreach (var seed in DefaultPlans)
        {
            if (existing.Contains(seed.Code))
                continue;

            toInsert.Add(SaasPlan.Create(
                code: seed.Code,
                name: seed.Name,
                shortLabel: seed.ShortLabel,
                isActive: true,
                priceAmount: seed.PriceAmount,
                currency: "USD",
                billingCycle: SaasBillingCycle.Monthly,
                isPubliclyVisible: true,
                isRecommended: seed.IsRecommended,
                sortOrder: seed.SortOrder,
                externalBillingRef: null));
        }

        if (toInsert.Count == 0)
            return;

        db.SaasPlans.AddRange(toInsert);
        await db.SaveChangesAsync(ct);
    }
}


using Microsoft.EntityFrameworkCore;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

/// <summary>Resolución de plan, overrides y consumo por tenant (PostgreSQL / EF Core).</summary>
public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ErpDbContext _db;

    public SubscriptionService(ErpDbContext db) => _db = db;

    public async Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return true;

        var code = NormalizeFeatureCode(featureCode);
        var feature = await _db.SaasFeatureDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == code, ct);
        if (feature is null) return false;

        var sub = await GetActiveSubscriptionRowAsync(tenantId, ct);
        if (sub is null) return false;

        var overrideRow = await _db.TenantSubscriptionFeatureOverrides.AsNoTracking()
            .FirstOrDefaultAsync(o => o.SubscriptionId == sub.Id && o.FeatureId == feature.Id, ct);
        if (overrideRow is not null && !overrideRow.IsEnabled)
            return false;

        var inPlan = await _db.SaasPlanFeatures.AsNoTracking()
            .AnyAsync(pf => pf.PlanId == sub.PlanId && pf.FeatureId == feature.Id && pf.IsIncluded, ct);
        if (inPlan) return true;

        return overrideRow is { IsEnabled: true };
    }

    public async Task<bool> CheckLimitAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return true;
        if (amount <= 0) return true;

        var code = NormalizeFeatureCode(featureCode);
        var feature = await _db.SaasFeatureDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == code, ct);
        if (feature is null || !feature.IsMetered) return true;

        if (!await HasFeatureAsync(tenantId, code, ct)) return false;

        var limit = await GetEffectiveLimitAsync(tenantId, feature.Id, ct);
        if (limit is null) return true;

        var period = MonthlyPeriodKey(DateTime.UtcNow);
        var used = await _db.TenantSubscriptionUsages.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.FeatureId == feature.Id && u.PeriodKey == period)
            .Select(u => u.Quantity)
            .FirstOrDefaultAsync(ct);

        return used + amount <= limit.Value;
    }

    public async Task IncrementUsageAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || amount <= 0) return;

        var code = NormalizeFeatureCode(featureCode);
        var feature = await _db.SaasFeatureDefinitions.FirstOrDefaultAsync(f => f.Code == code, ct);
        if (feature is null || !feature.IsMetered) return;

        var period = MonthlyPeriodKey(DateTime.UtcNow);
        var row = await _db.TenantSubscriptionUsages
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.FeatureId == feature.Id && u.PeriodKey == period, ct);

        if (row is null)
        {
            await _db.TenantSubscriptionUsages.AddAsync(
                TenantSubscriptionUsage.Create(tenantId, feature.Id, period, amount, Guid.Empty),
                ct);
        }
        else
        {
            row.AddQuantity(amount, Guid.Empty);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<TenantSaasSubscription?> GetActiveSubscriptionRowAsync(Guid tenantId, CancellationToken ct) =>
        await _db.TenantSaasSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == TenantSubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task<long?> GetEffectiveLimitAsync(Guid tenantId, Guid featureId, CancellationToken ct)
    {
        var sub = await GetActiveSubscriptionRowAsync(tenantId, ct);
        if (sub is null) return null;

        var ov = await _db.TenantSubscriptionFeatureOverrides.AsNoTracking()
            .FirstOrDefaultAsync(o => o.SubscriptionId == sub.Id && o.FeatureId == featureId, ct);
        if (ov?.LimitOverridePerPeriod is not null)
            return ov.LimitOverridePerPeriod;

        var pf = await _db.SaasPlanFeatures.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlanId == sub.PlanId && x.FeatureId == featureId, ct);
        return pf?.LimitPerPeriod;
    }

    private static string MonthlyPeriodKey(DateTime utc) => utc.ToString("yyyy-MM");

    private static string NormalizeFeatureCode(string featureCode) =>
        (featureCode ?? string.Empty).Trim().ToUpperInvariant();
}

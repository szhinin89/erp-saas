using Microsoft.EntityFrameworkCore;
using ERP.Application.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Subscriptions.Interfaces;
using ERP.Infrastructure.Persistence;

namespace ERP.Infrastructure.Services;

/// <summary>Resolución de plan, overrides y consumo por tenant (PostgreSQL / EF Core).</summary>
public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ErpDbContext _db;
    private readonly ITenantEntitlementsService _entitlements;

    public SubscriptionService(ErpDbContext db, ITenantEntitlementsService entitlements)
    {
        _db = db;
        _entitlements = entitlements;
    }

    public Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default) =>
        _entitlements.HasFeatureAsync(tenantId, featureCode, ct);

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

    public async Task<bool> IncrementUsageAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty || amount <= 0) return false;

        var code = NormalizeFeatureCode(featureCode);
        var feature = await _db.SaasFeatureDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == code, ct);
        if (feature is null || !feature.IsMetered) return false;

        var period = MonthlyPeriodKey(DateTime.UtcNow);
        return await SubscriptionUsageIncrementer.IncrementAsync(
            _db, tenantId, feature.Id, period, amount, ct);
    }

    private async Task<TenantSaasSubscription?> GetActiveSubscriptionRowAsync(Guid tenantId, CancellationToken ct) =>
        await _db.TenantSaasSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == TenantSubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task<long?> GetEffectiveLimitAsync(Guid tenantId, Guid featureId, CancellationToken ct)
    {
        var (planId, subscriptionId) = await ResolveEffectivePlanAndSubscriptionAsync(tenantId, ct);
        if (planId is null) return null;

        if (subscriptionId is not null)
        {
            var ov = await _db.TenantSubscriptionFeatureOverrides.AsNoTracking()
                .FirstOrDefaultAsync(o => o.SubscriptionId == subscriptionId.Value && o.FeatureId == featureId, ct);
            if (ov?.LimitOverridePerPeriod is not null)
                return ov.LimitOverridePerPeriod;
        }

        var pf = await _db.SaasPlanFeatures.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlanId == planId.Value && x.FeatureId == featureId, ct);
        return pf?.LimitPerPeriod;
    }

    private async Task<(Guid? PlanId, Guid? SubscriptionId)> ResolveEffectivePlanAndSubscriptionAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        var sub = await GetActiveSubscriptionRowAsync(tenantId, ct);
        if (sub is not null)
            return (sub.PlanId, sub.Id);

        var planCode = await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => t.PlanCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(planCode))
            return (null, null);

        var normalizedCode = planCode.Trim().ToLowerInvariant();

        var planId = await _db.SaasPlans.AsNoTracking()
            .Where(p => p.Code.ToLower() == normalizedCode)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        return (planId, null);
    }

    private static string MonthlyPeriodKey(DateTime utc) => utc.ToString("yyyy-MM");

    private static string NormalizeFeatureCode(string featureCode) =>
        (featureCode ?? string.Empty).Trim().ToUpperInvariant();
}

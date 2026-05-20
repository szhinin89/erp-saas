using ERP.Application.Common;
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
    private readonly ISubscriberEntitlementsService _entitlements;
    private readonly IPlatformQueryAccessor _platform;

    public SubscriptionService(
        ErpDbContext db,
        ISubscriberEntitlementsService entitlements,
        IPlatformQueryAccessor platform)
    {
        _db = db;
        _entitlements = entitlements;
        _platform = platform;
    }

    public Task<bool> HasFeatureAsync(Guid subscriberId, string featureCode, CancellationToken ct = default) =>
        _entitlements.HasFeatureAsync(subscriberId, featureCode, ct);

    public async Task<bool> CheckLimitAsync(Guid subscriberId, string featureCode, long amount = 1, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty) return true;
        if (amount <= 0) return true;

        var code = NormalizeFeatureCode(featureCode);
        var feature = await _db.PlatformFeatures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == code, ct);
        if (feature is null || !feature.IsMetered) return true;

        if (!await HasFeatureAsync(subscriberId, code, ct)) return false;

        var limit = await GetEffectiveLimitAsync(subscriberId, feature.Id, ct);
        if (limit is null) return true;

        var period = MonthlyPeriodKey(DateTime.UtcNow);
        var used = await _platform
            .Unfiltered(_db.SubscriptionUsages.AsNoTracking(), PlatformQueryReason.TenantScopedExplicit)
            .Where(u => u.SubscriberId == subscriberId && u.FeatureId == feature.Id && u.PeriodKey == period)
            .Select(u => u.Quantity)
            .FirstOrDefaultAsync(ct);

        return used + amount <= limit.Value;
    }

    public async Task<bool> IncrementUsageAsync(Guid subscriberId, string featureCode, long amount = 1, CancellationToken ct = default)
    {
        if (subscriberId == Guid.Empty || amount <= 0) return false;

        var code = NormalizeFeatureCode(featureCode);
        var feature = await _db.PlatformFeatures.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == code, ct);
        if (feature is null || !feature.IsMetered) return false;

        var period = MonthlyPeriodKey(DateTime.UtcNow);
        return await SubscriptionUsageIncrementer.IncrementAsync(
            _db, subscriberId, feature.Id, period, amount, ct);
    }

    private async Task<SubscriberSubscription?> GetActiveSubscriptionRowAsync(Guid subscriberId, CancellationToken ct) =>
        await _platform
            .Unfiltered(_db.SubscriberSubscriptions.AsNoTracking(), PlatformQueryReason.TenantScopedExplicit)
            .Where(s => s.SubscriberId == subscriberId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

    private async Task<long?> GetEffectiveLimitAsync(Guid subscriberId, Guid featureId, CancellationToken ct)
    {
        var (planId, subscriptionId) = await ResolveEffectivePlanAndSubscriptionAsync(subscriberId, ct);
        if (planId is null) return null;

        if (subscriptionId is not null)
        {
            var ov = await _platform
                .Unfiltered(_db.SubscriptionFeatureOverrides.AsNoTracking(), PlatformQueryReason.TenantScopedExplicit)
                .FirstOrDefaultAsync(
                    o => o.SubscriberId == subscriberId && o.SubscriptionId == subscriptionId.Value && o.FeatureId == featureId,
                    ct);
            if (ov?.LimitOverridePerPeriod is not null)
                return ov.LimitOverridePerPeriod;
        }

        var pf = await _db.CommercialPlanFeatures.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlanId == planId.Value && x.FeatureId == featureId, ct);
        return pf?.LimitPerPeriod;
    }

    private async Task<(Guid? PlanId, Guid? SubscriptionId)> ResolveEffectivePlanAndSubscriptionAsync(
        Guid subscriberId,
        CancellationToken ct)
    {
        var sub = await GetActiveSubscriptionRowAsync(subscriberId, ct);
        if (sub is not null)
            return (sub.PlanId, sub.Id);

        var planCode = await _db.Subscribers.AsNoTracking()
            .Where(t => t.Id == subscriberId)
            .Select(t => t.PlanCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(planCode))
            return (null, null);

        var normalizedCode = planCode.Trim().ToLowerInvariant();

        var planId = await _db.CommercialPlans.AsNoTracking()
            .Where(p => p.Code.ToLower() == normalizedCode)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        return (planId, null);
    }

    private static string MonthlyPeriodKey(DateTime utc) => utc.ToString("yyyy-MM");

    private static string NormalizeFeatureCode(string featureCode) =>
        (featureCode ?? string.Empty).Trim().ToUpperInvariant();
}

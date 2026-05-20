using System.Data;
using ERP.Application.Common;
using ERP.Application.Subscriptions.CommercialPlanLimits;
using ERP.Domain.Subscriptions;
using ERP.Domain.Subscriptions.Entities;
using ERP.Domain.Subscriptions.Exceptions;
using ERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Services;

public sealed class CommercialPlanLimitService : ICommercialPlanLimitService
{
    private readonly ErpDbContext _db;
    private readonly IPlatformQueryAccessor _platform;
    private readonly IReadOnlyList<ICommercialLimitUsageProvider> _usageProviders;

    public CommercialPlanLimitService(
        ErpDbContext db,
        IPlatformQueryAccessor platform,
        IEnumerable<ICommercialLimitUsageProvider> usageProviders)
    {
        _db = db;
        _platform = platform;
        _usageProviders = usageProviders.ToList();
    }

    public async Task<CommercialLimitsSnapshot> GetEffectiveLimitsSnapshotAsync(
        Guid subscriberId,
        CancellationToken ct = default)
    {
        var (planId, planCode) = await ResolveCommercialPlanAsync(subscriberId, ct);
        var limits = planId == Guid.Empty
            ? new Dictionary<string, EffectiveCommercialLimit>(StringComparer.OrdinalIgnoreCase)
            : await LoadPlanLimitsAsync(planId, planCode, ct);

        return new CommercialLimitsSnapshot(
            subscriberId,
            planId == Guid.Empty ? null : planId,
            planCode,
            limits,
            DateTime.UtcNow);
    }

    public async Task<CommercialLimitEvaluationResult> EvaluateAsync(
        Guid subscriberId,
        string limitCode,
        long increment = 1,
        CancellationToken ct = default)
    {
        var normalized = NormalizeLimitCode(limitCode);
        if (normalized.Length == 0)
            return CommercialLimitEvaluationResult.Deny(normalized, 0, null, increment, true, "Invalid limit code.");

        var (planId, planCode) = await ResolveCommercialPlanAsync(subscriberId, ct);
        if (planId == Guid.Empty)
        {
            return CommercialLimitEvaluationResult.Deny(
                normalized, 0, null, increment, true,
                "No active commercial plan for this subscriber.");
        }

        var limit = await _db.CommercialPlanLimits.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CommercialPlanId == planId &&
                     x.LimitCode == normalized,
                ct);

        if (limit is null)
        {
            return CommercialLimitEvaluationResult.Allow(
                normalized, 0, null, increment, isHardLimit: true);
        }

        if (limit.LimitValue <= 0)
        {
            var usageUnlimited = await GetCurrentUsageAsync(subscriberId, normalized, ct);
            return CommercialLimitEvaluationResult.Allow(
                normalized, usageUnlimited, null, increment, limit.IsHardLimit);
        }

        var current = await GetCurrentUsageAsync(subscriberId, normalized, ct);
        var wouldBe = current + increment;
        if (wouldBe > limit.LimitValue)
        {
            return CommercialLimitEvaluationResult.Deny(
                normalized,
                current,
                limit.LimitValue,
                increment,
                limit.IsHardLimit,
                CommercialPlanLimitExceededException.BuildDefaultMessage(normalized));
        }

        return CommercialLimitEvaluationResult.Allow(
            normalized, current, limit.LimitValue, increment, limit.IsHardLimit);
    }

    public async Task EnsureCanIncrementAsync(
        Guid subscriberId,
        string limitCode,
        long increment = 1,
        CancellationToken ct = default)
    {
        var eval = await EvaluateAsync(subscriberId, limitCode, increment, ct);
        if (eval.Allowed)
            return;

        throw new CommercialPlanLimitExceededException(
            eval.LimitCode,
            eval.LimitValue,
            eval.CurrentUsage,
            eval.RequestedIncrement,
            eval.DenialReason);
    }

    public async Task ExecuteWithLimitEnforcementAsync(
        Guid subscriberId,
        string limitCode,
        long increment,
        Func<CancellationToken, Task> action,
        CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await LockSubscriberRowAsync(subscriberId, ct);

            var eval = await EvaluateAsync(subscriberId, limitCode, increment, ct);
            if (!eval.Allowed)
            {
                throw new CommercialPlanLimitExceededException(
                    eval.LimitCode,
                    eval.LimitValue,
                    eval.CurrentUsage,
                    eval.RequestedIncrement,
                    eval.DenialReason);
            }

            await action(ct);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private async Task LockSubscriberRowAsync(Guid subscriberId, CancellationToken ct)
    {
        if (!_db.Database.IsRelational())
            return;

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT id FROM subscribers WHERE id = {subscriberId} FOR UPDATE
             """,
            ct);
    }

    private async Task<long> GetCurrentUsageAsync(Guid subscriberId, string limitCode, CancellationToken ct)
    {
        var provider = _usageProviders.FirstOrDefault(p => p.Supports(limitCode));
        if (provider is null)
            return 0;

        return await provider.GetCurrentUsageAsync(subscriberId, ct);
    }

    private async Task<(Guid PlanId, string? PlanCode)> ResolveCommercialPlanAsync(
        Guid subscriberId,
        CancellationToken ct)
    {
        var subscription = await _platform
            .Unfiltered(_db.SubscriberSubscriptions.AsNoTracking(), PlatformQueryReason.TenantScopedExplicit)
            .Where(s => s.SubscriberId == subscriberId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.StartedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (subscription is not null)
        {
            var plan = await _db.CommercialPlans.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == subscription.PlanId, ct);
            return (subscription.PlanId, plan?.Code);
        }

        var subscriber = await _db.Subscribers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == subscriberId, ct);
        if (subscriber is null || string.IsNullOrWhiteSpace(subscriber.PlanCode))
            return (Guid.Empty, null);

        var code = subscriber.PlanCode.Trim().ToLowerInvariant();
        var planByCode = await _db.CommercialPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == code && p.IsActive, ct);

        return planByCode is null ? (Guid.Empty, code) : (planByCode.Id, planByCode.Code);
    }

    private async Task<Dictionary<string, EffectiveCommercialLimit>> LoadPlanLimitsAsync(
        Guid planId,
        string? planCode,
        CancellationToken ct)
    {
        var rows = await _db.CommercialPlanLimits.AsNoTracking()
            .Where(x => x.CommercialPlanId == planId)
            .ToListAsync(ct);

        var dict = new Dictionary<string, EffectiveCommercialLimit>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            dict[row.LimitCode] = new EffectiveCommercialLimit(
                row.LimitCode,
                row.LimitValue,
                row.PeriodType,
                row.IsHardLimit,
                planId,
                planCode);
        }

        return dict;
    }

    private static string NormalizeLimitCode(string limitCode) =>
        (limitCode ?? string.Empty).Trim().ToUpperInvariant();
}

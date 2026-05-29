using ERP.Application.Common.Features;
using ERP.Application.Subscriptions;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Implements IFeatureAccessService by delegating to ISubscriberEntitlementsService.
/// Single source of truth for "what can this tenant do?" queries.
/// </summary>
public sealed class FeatureAccessService : IFeatureAccessService
{
    private readonly ISubscriberEntitlementsService _entitlements;
    private readonly ILogger<FeatureAccessService>  _log;

    public FeatureAccessService(
        ISubscriberEntitlementsService entitlements,
        ILogger<FeatureAccessService> log)
    {
        _entitlements = entitlements;
        _log          = log;
    }

    public async Task<bool> HasFeatureAsync(Guid subscriberId, string featureKey, CancellationToken ct = default)
    {
        try   { return await _entitlements.HasFeatureAsync(subscriberId, featureKey, ct); }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeatureAccessService: feature={Feature} subscriber={Id}", featureKey, subscriberId);
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetEnabledModulesAsync(Guid subscriberId, CancellationToken ct = default)
    {
        try
        {
            var modules = await _entitlements.GetEnabledModuleKeysAsync(subscriberId, ct);
            return modules.ToList();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeatureAccessService: GetModules subscriber={Id}", subscriberId);
            return [];
        }
    }

    public async Task<int?> GetQuotaLimitAsync(Guid subscriberId, string quotaCode, CancellationToken ct = default)
    {
        try
        {
            var limit = await _entitlements.GetLimitPerPeriodAsync(subscriberId, quotaCode, ct);
            return limit.HasValue ? (int?)limit.Value : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeatureAccessService: GetQuotaLimit quota={Code} subscriber={Id}", quotaCode, subscriberId);
            return null;
        }
    }

    public Task<int> GetQuotaUsageAsync(Guid subscriberId, string quotaCode, CancellationToken ct = default)
        => Task.FromResult(0); // Delegate to CommercialPlanLimitService for precise usage

    public async Task<bool> CanIncrementQuotaAsync(Guid subscriberId, string quotaCode, int increment = 1, CancellationToken ct = default)
    {
        var limit = await GetQuotaLimitAsync(subscriberId, quotaCode, ct);
        if (!limit.HasValue || limit.Value <= 0) return true;
        return (await GetQuotaUsageAsync(subscriberId, quotaCode, ct) + increment) <= limit.Value;
    }

    public async Task<FeatureAccessSnapshot> GetSnapshotAsync(Guid subscriberId, CancellationToken ct = default)
    {
        try
        {
            var s = await _entitlements.GetEntitlementsSnapshotAsync(subscriberId, ct);
            return new FeatureAccessSnapshot
            {
                SubscriberId    = subscriberId,
                PlanCode        = s.PlanCode,
                EnabledModules  = s.EnabledModules,
                EnabledFeatures = s.EnabledFeatures,
                QuotaLimits     = s.CommercialLimits is not null
                    ? s.CommercialLimits
                        .Where(kv => !kv.Value.IsUnlimited)
                        .ToDictionary(kv => kv.Key, kv => (int)kv.Value.LimitValue)
                    : new Dictionary<string, int>(),
                ResolvedAtUtc   = s.ResolvedAtUtc ?? DateTime.UtcNow,
            };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FeatureAccessService: GetSnapshot subscriber={Id}", subscriberId);
            return new FeatureAccessSnapshot { SubscriberId = subscriberId, ResolvedAtUtc = DateTime.UtcNow };
        }
    }
}

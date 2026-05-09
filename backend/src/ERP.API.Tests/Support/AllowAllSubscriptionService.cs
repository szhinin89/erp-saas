using ERP.Domain.Subscriptions.Interfaces;

namespace ERP.API.Tests.Support;

internal sealed class AllowAllSubscriptionService : ISubscriptionService
{
    public Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<bool> CheckLimitAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task IncrementUsageAsync(Guid tenantId, string featureCode, long amount = 1, CancellationToken ct = default)
        => Task.CompletedTask;
}

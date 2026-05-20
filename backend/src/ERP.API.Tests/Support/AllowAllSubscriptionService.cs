using ERP.Domain.Subscriptions.Interfaces;

namespace ERP.API.Tests.Support;

internal sealed class AllowAllSubscriptionService : ISubscriptionService
{
    public Task<bool> HasFeatureAsync(Guid subscriberId, string featureCode, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<bool> CheckLimitAsync(Guid subscriberId, string featureCode, long amount = 1, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<bool> IncrementUsageAsync(Guid subscriberId, string featureCode, long amount = 1, CancellationToken ct = default)
        => Task.FromResult(false);
}

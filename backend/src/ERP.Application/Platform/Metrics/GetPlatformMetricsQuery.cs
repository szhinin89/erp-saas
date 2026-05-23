using ERP.Application.Common;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;
using MediatR;

namespace ERP.Application.Platform.Metrics;

public sealed record PlatformMetricsDto(
    PlatformSubscriberMetrics Subscribers,
    PlatformPlanDistributionDto Plans,
    PlatformLifecycleDistributionDto Lifecycle);

public sealed record PlatformSubscriberMetrics(
    int Total,
    int Active,
    int Trial,
    int GracePeriod,
    int Suspended,
    int Inactive);

public sealed record PlatformPlanDistributionDto(
    IReadOnlyList<PlanCountDto> ByPlan);

public sealed record PlanCountDto(
    string PlanCode,
    int Count);

public sealed record PlatformLifecycleDistributionDto(
    int ActiveCount,
    int TrialCount,
    int GracePeriodCount,
    int SuspendedCount,
    int InactiveCount);

public sealed record GetPlatformMetricsQuery : IRequest<Result<PlatformMetricsDto>>;

public sealed class GetPlatformMetricsHandler : IRequestHandler<GetPlatformMetricsQuery, Result<PlatformMetricsDto>>
{
    private readonly ISubscriberRepository _subscribers;

    public GetPlatformMetricsHandler(ISubscriberRepository subscribers)
        => _subscribers = subscribers;

    public async Task<Result<PlatformMetricsDto>> Handle(GetPlatformMetricsQuery request, CancellationToken ct)
    {
        var all = await _subscribers.GetAllAsync(ct);

        var activeCount = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Active);
        var trialCount = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Trial);
        var graceCount = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.GracePeriod);
        var suspendedCount = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Suspended);
        var inactiveCount = all.Count(s => s.LifecycleStatus == SubscriberLifecycleStatus.Inactive);

        var planDistribution = all
            .GroupBy(s => s.PlanCode ?? "none")
            .Select(g => new PlanCountDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        return Result<PlatformMetricsDto>.Success(new PlatformMetricsDto(
            Subscribers: new PlatformSubscriberMetrics(
                Total: all.Count,
                Active: activeCount,
                Trial: trialCount,
                GracePeriod: graceCount,
                Suspended: suspendedCount,
                Inactive: inactiveCount),
            Plans: new PlatformPlanDistributionDto(planDistribution),
            Lifecycle: new PlatformLifecycleDistributionDto(
                activeCount, trialCount, graceCount, suspendedCount, inactiveCount)));
    }
}

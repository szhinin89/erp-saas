using ERP.Application.Common;
using MediatR;

namespace ERP.Application.Platform.Subscribers.UseCases;

public record ActivateSubscriberCommand(
    Guid SubscriberId,
    string? Notes = null
) : IRequest<Result<bool>>;

public record SuspendSubscriberCommand(
    Guid SubscriberId,
    string? Reason = null
) : IRequest<Result<bool>>;

public record SetSubscriberTrialCommand(
    Guid SubscriberId,
    DateTime TrialEndsAtUtc
) : IRequest<Result<bool>>;

public record SetSubscriberGracePeriodCommand(
    Guid SubscriberId,
    DateTime GracePeriodEndsAtUtc,
    string? Reason = null
) : IRequest<Result<bool>>;

public record ChangePlatformSubscriberPlanCommand(
    Guid SubscriberId,
    string NewPlanCode,
    string? Notes = null
) : IRequest<Result<bool>>;

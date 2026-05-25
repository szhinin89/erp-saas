using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberSubscription;

public sealed record UpdateSubscriberSubscriptionCommand(
    Guid SubscriberId,
    string? PlanCode,
    IReadOnlyList<string>? EnabledModules
) : IRequest<Result<SubscriberDto>>;

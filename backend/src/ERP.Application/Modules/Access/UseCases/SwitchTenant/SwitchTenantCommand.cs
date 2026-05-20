using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.SwitchSubscriber;

public record SwitchSubscriberCommand(
    Guid SubscriberId
) : IRequest<Result<SessionResponseDto>>;


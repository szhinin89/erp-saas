using MediatR;
using ERP.Application.Common;
using ERP.Application.Access.DTOs;

namespace ERP.Application.Access.UseCases.BootstrapSwitchSubscriber;

public record BootstrapSwitchSubscriberCommand(
    Guid SubscriberId
) : IRequest<Result<SessionResponseDto>>;

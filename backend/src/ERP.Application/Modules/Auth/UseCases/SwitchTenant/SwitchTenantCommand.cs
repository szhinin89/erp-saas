using MediatR;
using ERP.Application.Common;
using ERP.Application.Auth.DTOs;

namespace ERP.Application.Auth.UseCases.SwitchSubscriber;

public record SwitchSubscriberCommand(Guid SubscriberId) : IRequest<Result<AuthResponseDto>>;

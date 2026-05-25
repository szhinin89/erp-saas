using MediatR;
using ERP.Application.Common;

namespace ERP.Application.Subscribers.UseCases.UpdatePasswordResetMode;

public record UpdateSubscriberPasswordResetModeCommand(
    Guid SubscriberId,
    ERP.Domain.Subscribers.Entities.PasswordResetMode PasswordResetMode
) : IRequest<Result<bool>>;

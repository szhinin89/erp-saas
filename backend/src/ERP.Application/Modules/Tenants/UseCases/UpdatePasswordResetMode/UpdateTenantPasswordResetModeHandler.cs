using MediatR;
using ERP.Application.Common;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Subscribers.UseCases.UpdatePasswordResetMode;

public class UpdateSubscriberPasswordResetModeHandler : IRequestHandler<UpdateSubscriberPasswordResetModeCommand, Result<bool>>
{
    private readonly ISubscriberRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateSubscriberPasswordResetModeHandler(ISubscriberRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<bool>> Handle(UpdateSubscriberPasswordResetModeCommand command, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            return Result<bool>.Failure("No autenticado.");

        var tenant = await _repository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<bool>.Failure("Empresa no encontrada.");

        tenant.SetPasswordResetMode(command.PasswordResetMode, updatedBy: _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}


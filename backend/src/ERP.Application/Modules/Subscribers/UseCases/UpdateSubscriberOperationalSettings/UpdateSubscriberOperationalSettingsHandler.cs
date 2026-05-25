using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberOperationalSettings;

public sealed class UpdateSubscriberOperationalSettingsHandler
    : IRequestHandler<UpdateSubscriberOperationalSettingsCommand, Result<SubscriberDto>>
{
    private readonly ISubscriberRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentSubscriber _currentSubscriber;

    public UpdateSubscriberOperationalSettingsHandler(
        ISubscriberRepository repository,
        ICurrentUser currentUser,
        ICurrentSubscriber currentSubscriber)
    {
        _repository    = repository;
        _currentUser   = currentUser;
        _currentSubscriber = currentSubscriber;
    }

    public async Task<Result<SubscriberDto>> Handle(
        UpdateSubscriberOperationalSettingsCommand command,
        CancellationToken ct)
    {
        var tenant = await _repository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<SubscriberDto>.Failure("Empresa no encontrada.");

        // Un Admin solo puede editar su propia empresa
        if (_currentUser.Role == "Admin" && _currentSubscriber.SubscriberId != command.SubscriberId)
            return Result<SubscriberDto>.Failure("No tiene permiso para modificar esta empresa.");

        tenant.UpdateOperationalSettings(
            command.Currency,
            command.Language,
            command.Timezone,
            command.InvoicePrefix,
            command.DefaultCreditDays,
            _currentUser.UserId);

        await _repository.SaveChangesAsync(ct);

        return Result<SubscriberDto>.Success(SubscriberDto.FromSubscriber(tenant));
    }
}

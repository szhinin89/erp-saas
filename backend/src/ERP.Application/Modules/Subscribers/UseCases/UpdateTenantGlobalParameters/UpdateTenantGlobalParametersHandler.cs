using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberGlobalParameters;

public sealed class UpdateSubscriberGlobalParametersHandler : IRequestHandler<UpdateSubscriberGlobalParametersCommand, Result<SubscriberDto>>
{
    private readonly ISubscriberRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateSubscriberGlobalParametersHandler(ISubscriberRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<SubscriberDto>> Handle(UpdateSubscriberGlobalParametersCommand command, CancellationToken ct)
    {
        var tenant = await _repository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<SubscriberDto>.Failure("Empresa no encontrada.");

        if (string.IsNullOrWhiteSpace(tenant.PlanCode))
            return Result<SubscriberDto>.Failure("La empresa no tiene un plan comercial asignado.");

        tenant.UpdateGlobalParameters(command.ElectronicBillingTrialEnabled, _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        return Result<SubscriberDto>.Success(SubscriberDto.FromTenant(tenant));
    }
}


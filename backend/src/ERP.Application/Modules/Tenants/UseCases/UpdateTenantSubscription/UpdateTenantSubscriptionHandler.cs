using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Subscribers.UseCases.UpdateSubscriberSubscription;

public sealed class UpdateSubscriberSubscriptionHandler : IRequestHandler<UpdateSubscriberSubscriptionCommand, Result<SubscriberDto>>
{
    private readonly ISubscriberRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ISubscriptionFeatureOverridesService _overrides;

    public UpdateSubscriberSubscriptionHandler(
        ISubscriberRepository repository,
        ICurrentUser currentUser,
        ISessionModulesResolver sessionModules,
        ISubscriptionFeatureOverridesService overrides)
    {
        _repository = repository;
        _currentUser = currentUser;
        _sessionModules = sessionModules;
        _overrides = overrides;
    }

    public async Task<Result<SubscriberDto>> Handle(UpdateSubscriberSubscriptionCommand command, CancellationToken ct)
    {
        if (command.EnabledModules is { Count: > 0 })
            SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(command.EnabledModules);

        var tenant = await _repository.GetByIdAsync(command.SubscriberId, ct);
        if (tenant is null)
            return Result<SubscriberDto>.Failure("Empresa no encontrada.");

        tenant.SetPlanCode(command.PlanCode, _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        await _overrides.ApplyModuleOverridesAsync(
            tenant.Id,
            command.EnabledModules is { Count: > 0 }
                ? SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(command.EnabledModules)
                : null,
            _currentUser.UserId,
            ct);
        await _repository.SaveChangesAsync(ct);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);
        return Result<SubscriberDto>.Success(SubscriberDto.FromTenant(tenant, modules));
    }
}

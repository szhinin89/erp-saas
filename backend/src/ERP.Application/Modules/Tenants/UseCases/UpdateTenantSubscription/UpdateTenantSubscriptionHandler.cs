using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.UpdateTenantSubscription;

public sealed class UpdateTenantSubscriptionHandler : IRequestHandler<UpdateTenantSubscriptionCommand, Result<TenantDto>>
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ISessionModulesResolver _sessionModules;
    private readonly ITenantSubscriptionOverridesService _overrides;

    public UpdateTenantSubscriptionHandler(
        ITenantRepository repository,
        ICurrentUser currentUser,
        ISessionModulesResolver sessionModules,
        ITenantSubscriptionOverridesService overrides)
    {
        _repository = repository;
        _currentUser = currentUser;
        _sessionModules = sessionModules;
        _overrides = overrides;
    }

    public async Task<Result<TenantDto>> Handle(UpdateTenantSubscriptionCommand command, CancellationToken ct)
    {
        if (command.EnabledModules is { Count: > 0 })
            TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(command.EnabledModules);

        var tenant = await _repository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null)
            return Result<TenantDto>.Failure("Empresa no encontrada.");

        tenant.SetPlanCode(command.PlanCode, _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        await _overrides.ApplyModuleOverridesAsync(
            tenant.Id,
            command.EnabledModules is { Count: > 0 }
                ? TenantSubscriptionCatalog.NormalizeModuleKeysInput(command.EnabledModules)
                : null,
            _currentUser.UserId,
            ct);
        await _repository.SaveChangesAsync(ct);

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, tenant, ct);
        return Result<TenantDto>.Success(TenantDto.FromTenant(tenant, modules));
    }
}

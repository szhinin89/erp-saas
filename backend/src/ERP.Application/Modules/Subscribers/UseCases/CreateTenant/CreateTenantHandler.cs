using MediatR;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Application.Subscribers.DTOs;
using ERP.Domain.Subscribers.Entities;
using ERP.Domain.Subscribers.Interfaces;

namespace ERP.Application.Subscribers.UseCases.CreateSubscriber;

public class CreateSubscriberHandler : IRequestHandler<CreateSubscriberCommand, Result<SubscriberDto>>
{
    private readonly ISubscriberRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;
    private readonly ISubscriptionFeatureOverridesService _overrides;
    private readonly ISessionModulesResolver _sessionModules;

    public CreateSubscriberHandler(
        ISubscriberRepository repository,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment,
        ISubscriptionFeatureOverridesService overrides,
        ISessionModulesResolver sessionModules)
    {
        _repository = repository;
        _currentUser = currentUser;
        _deployment = deployment;
        _overrides = overrides;
        _sessionModules = sessionModules;
    }

    public async Task<Result<SubscriberDto>> Handle(
        CreateSubscriberCommand command,
        CancellationToken ct)
    {
        var exists = await _repository.GetBySlugAsync(command.Slug, ct);
        if (exists is not null)
            return Result<SubscriberDto>.Failure($"Ya existe un tenant con el slug '{command.Slug}'.");

        var quotaMsg = await DeploymentQuota.GetBlockingReasonIfAtActiveSubscriberCapAsync(_deployment, _repository, ct);
        if (quotaMsg is not null)
            return Result<SubscriberDto>.Failure(quotaMsg);

        if (command.EnabledModules is { Count: > 0 })
            SubscriberSubscriptionCatalog.ValidateModuleKeysOrThrow(command.EnabledModules);

        var tenant = Subscriber.Create(
            command.Name,
            command.Slug,
            _currentUser.UserId,
            command.PasswordResetMode,
            ruc: command.Ruc,
            shortName: command.ShortName,
            tradeName: command.TradeName,
            dinardap: command.Dinardap,
            logoUrl: command.LogoUrl,
            displayOrder: command.DisplayOrder,
            priority: command.Priority,
            planCode: command.PlanCode);

        await _repository.AddAsync(tenant, ct);
        await _repository.SaveChangesAsync(ct);

        if (command.EnabledModules is { Count: > 0 })
        {
            await _overrides.ApplyModuleOverridesAsync(
                tenant.Id,
                SubscriberSubscriptionCatalog.NormalizeModuleKeysInput(command.EnabledModules),
                _currentUser.UserId,
                ct);
            await _repository.SaveChangesAsync(ct);
        }

        var modules = await _sessionModules.GetEnabledModuleKeysAsync(tenant.Id, ct);
        return Result<SubscriberDto>.Success(SubscriberDto.FromTenant(tenant, modules));
    }
}

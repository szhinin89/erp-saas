using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.CreateTenant;

public class CreateTenantHandler
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IDeploymentFeatureFlags _deployment;

    public CreateTenantHandler(
        ITenantRepository repository,
        ICurrentUser currentUser,
        IDeploymentFeatureFlags deployment)
    {
        _repository = repository;
        _currentUser = currentUser;
        _deployment = deployment;
    }

    public async Task<Result<TenantDto>> HandleAsync(
        CreateTenantCommand command,
        CancellationToken ct = default)
    {
        var exists = await _repository.GetBySlugAsync(command.Slug, ct);
        if (exists is not null)
            return Result<TenantDto>.Failure($"Ya existe un tenant con el slug '{command.Slug}'.");

        var quotaMsg = await DeploymentQuota.GetBlockingReasonIfAtActiveTenantCapAsync(_deployment, _repository, ct);
        if (quotaMsg is not null)
            return Result<TenantDto>.Failure(quotaMsg);

        if (command.EnabledModules is { Count: > 0 })
            TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(command.EnabledModules);

        var tenant = Tenant.Create(
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
            planCode: command.PlanCode,
            enabledModuleKeys: command.EnabledModules);

        await _repository.AddAsync(tenant, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.Ruc,
            tenant.ShortName,
            tenant.TradeName,
            tenant.Dinardap,
            tenant.LogoUrl,
            tenant.DisplayOrder,
            tenant.Priority,
            tenant.PlanCode,
            TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant)));
    }
}

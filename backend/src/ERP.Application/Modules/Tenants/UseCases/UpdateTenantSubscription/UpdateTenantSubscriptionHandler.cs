using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.UpdateTenantSubscription;

public sealed class UpdateTenantSubscriptionHandler
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateTenantSubscriptionHandler(ITenantRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<TenantDto>> HandleAsync(UpdateTenantSubscriptionCommand command, CancellationToken ct = default)
    {
        if (command.EnabledModules is { Count: > 0 })
            TenantSubscriptionCatalog.ValidateModuleKeysOrThrow(command.EnabledModules);

        var tenant = await _repository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null)
            return Result<TenantDto>.Failure("Empresa no encontrada.");

        tenant.SetSubscription(command.PlanCode, command.EnabledModules, _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(ToDto(tenant));
    }

    private static TenantDto ToDto(Tenant tenant) =>
        new(
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
            TenantSubscriptionCatalog.GetEffectiveEnabledModules(tenant));
}

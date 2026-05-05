using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.UpdateTenantGlobalParameters;

public sealed class UpdateTenantGlobalParametersHandler
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateTenantGlobalParametersHandler(ITenantRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Result<TenantDto>> HandleAsync(UpdateTenantGlobalParametersCommand command, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null)
            return Result<TenantDto>.Failure("Empresa no encontrada.");

        if (string.IsNullOrWhiteSpace(tenant.PlanCode))
            return Result<TenantDto>.Failure("La empresa no tiene un plan comercial asignado.");

        tenant.UpdateGlobalParameters(command.ElectronicBillingTrialEnabled, _currentUser.UserId);
        await _repository.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(TenantDto.FromTenant(tenant));
    }
}


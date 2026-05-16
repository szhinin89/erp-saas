using MediatR;
using ERP.Application.Common;
using ERP.Application.Tenants.DTOs;
using ERP.Domain.Tenants.Interfaces;

namespace ERP.Application.Tenants.UseCases.UpdateTenantOperationalSettings;

public sealed class UpdateTenantOperationalSettingsHandler
    : IRequestHandler<UpdateTenantOperationalSettingsCommand, Result<TenantDto>>
{
    private readonly ITenantRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentTenant _currentTenant;

    public UpdateTenantOperationalSettingsHandler(
        ITenantRepository repository,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentUser   = currentUser;
        _currentTenant = currentTenant;
    }

    public async Task<Result<TenantDto>> Handle(
        UpdateTenantOperationalSettingsCommand command,
        CancellationToken ct)
    {
        var tenant = await _repository.GetByIdAsync(command.TenantId, ct);
        if (tenant is null)
            return Result<TenantDto>.Failure("Empresa no encontrada.");

        // Un Admin solo puede editar su propia empresa
        if (_currentUser.Role == "Admin" && _currentTenant.TenantId != command.TenantId)
            return Result<TenantDto>.Failure("No tiene permiso para modificar esta empresa.");

        tenant.UpdateOperationalSettings(
            command.Currency,
            command.Language,
            command.Timezone,
            command.InvoicePrefix,
            command.DefaultCreditDays,
            _currentUser.UserId);

        await _repository.SaveChangesAsync(ct);

        return Result<TenantDto>.Success(TenantDto.FromTenant(tenant));
    }
}

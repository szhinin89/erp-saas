using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

public sealed class CreateConfiguracionGastoCategoriaCommandHandler
    : IRequestHandler<CreateConfiguracionGastoCategoriaCommand, Result<ConfiguracionGastoCategoriaDto>>
{
    private readonly IConfiguracionContableRepository _configRepo;
    private readonly IAccountingRepository            _accounts;
    private readonly ICurrentTenant                   _tenant;
    private readonly ICurrentUser                     _user;

    public CreateConfiguracionGastoCategoriaCommandHandler(
        IConfiguracionContableRepository configRepo,
        IAccountingRepository accounts,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
        _tenant     = tenant;
        _user       = user;
    }

    public async Task<Result<ConfiguracionGastoCategoriaDto>> Handle(
        CreateConfiguracionGastoCategoriaCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var dup      = await _configRepo.GetGastoCategoriaByCategoriaAsync(command.Categoria, ct);
        if (dup is not null)
            return Result<ConfiguracionGastoCategoriaDto>.Failure("Ya existe un mapeo para esa categoría.");

        var acc = await _accounts.GetByIdAsync(command.CuentaGastoId, tenantId, ct);
        if (acc is null)
            return Result<ConfiguracionGastoCategoriaDto>.Failure("La cuenta de gasto no existe o no pertenece al tenant.");
        if (!acc.IsActive)
            return Result<ConfiguracionGastoCategoriaDto>.Failure("La cuenta de gasto está deshabilitada.");
        if (!acc.AllowsMovements)
            return Result<ConfiguracionGastoCategoriaDto>.Failure("La cuenta es de agrupación; use una cuenta de detalle.");

        var entity = ConfiguracionGastoCategoria.Create(tenantId, command.Categoria, command.CuentaGastoId, _user.UserId);
        await _configRepo.AddGastoCategoriaAsync(entity, ct);
        await _configRepo.SaveChangesAsync(ct);

        return Result<ConfiguracionGastoCategoriaDto>.Success(
            new ConfiguracionGastoCategoriaDto(entity.Id, entity.Categoria, entity.CuentaGastoId));
    }
}

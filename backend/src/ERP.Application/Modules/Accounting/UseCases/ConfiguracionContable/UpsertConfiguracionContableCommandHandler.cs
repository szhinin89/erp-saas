using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

public sealed class UpsertConfiguracionContableCommandHandler
    : IRequestHandler<UpsertConfiguracionContableCommand, Result<ConfiguracionContableEmpresaDto>>
{
    private readonly IConfiguracionContableRepository _configRepo;
    private readonly IAccountingRepository            _accounts;
    private readonly ICurrentTenant                   _tenant;
    private readonly ICurrentUser                     _user;

    public UpsertConfiguracionContableCommandHandler(
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

    public async Task<Result<ConfiguracionContableEmpresaDto>> Handle(
        UpsertConfiguracionContableCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        foreach (var (role, id) in new (string, Guid?)[]
                 {
                     ("Inventario", command.CuentaInventarioId),
                     ("Costo de venta", command.CuentaCostoVentaId),
                     ("Proveedores", command.CuentaProveedoresId),
                     ("Ventas", command.CuentaVentasId),
                     ("Clientes", command.CuentaClientesId),
                     ("IVA compras", command.CuentaIvaComprasId),
                     ("IVA ventas", command.CuentaIvaVentasId),
                     ("Efectivo / caja", command.CuentaEfectivoId),
                     ("Banco", command.CuentaBancoId),
                 })
        {
            if (id is null)
                continue;
            var err = await ValidateAccountAsync(id.Value, tenantId, role, ct);
            if (err is not null)
                return Result<ConfiguracionContableEmpresaDto>.Failure(err);
        }

        var existing = await _configRepo.GetConfiguracionEmpresaAsync(ct);
        if (existing is null)
        {
            var created = ConfiguracionContableEmpresa.Create(tenantId, userId);
            created.UpdateCuentas(
                command.CuentaInventarioId,
                command.CuentaCostoVentaId,
                command.CuentaProveedoresId,
                command.CuentaVentasId,
                command.CuentaClientesId,
                command.CuentaIvaComprasId,
                command.CuentaIvaVentasId,
                command.CuentaEfectivoId,
                command.CuentaBancoId,
                userId);
            await _configRepo.AddConfiguracionEmpresaAsync(created, ct);
        }
        else
        {
            existing.UpdateCuentas(
                command.CuentaInventarioId,
                command.CuentaCostoVentaId,
                command.CuentaProveedoresId,
                command.CuentaVentasId,
                command.CuentaClientesId,
                command.CuentaIvaComprasId,
                command.CuentaIvaVentasId,
                command.CuentaEfectivoId,
                command.CuentaBancoId,
                userId);
        }

        await _configRepo.SaveChangesAsync(ct);

        var saved = await _configRepo.GetConfiguracionEmpresaAsync(ct);
        if (saved is null)
            return Result<ConfiguracionContableEmpresaDto>.Failure("No se pudo leer la configuración guardada.");

        return Result<ConfiguracionContableEmpresaDto>.Success(new ConfiguracionContableEmpresaDto(
            saved.CuentaInventarioId,
            saved.CuentaCostoVentaId,
            saved.CuentaProveedoresId,
            saved.CuentaVentasId,
            saved.CuentaClientesId,
            saved.CuentaIvaComprasId,
            saved.CuentaIvaVentasId,
            saved.CuentaEfectivoId,
            saved.CuentaBancoId));
    }

    private async Task<string?> ValidateAccountAsync(Guid accountId, Guid tenantId, string role, CancellationToken ct)
    {
        var a = await _accounts.GetByIdAsync(accountId, tenantId, ct);
        if (a is null)
            return $"La cuenta de {role} no existe o no pertenece al tenant.";
        if (!a.IsActive)
            return $"La cuenta de {role} está deshabilitada.";
        if (!a.AllowsMovements)
            return $"La cuenta de {role} es de agrupación; use una cuenta de detalle.";
        return null;
    }
}

using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Entities;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CuentaContableService : ICuentaContableService
{
    private readonly IConfiguracionContableRepository _configRepo;
    private readonly IAccountingRepository _accounts;

    public CuentaContableService(
        IConfiguracionContableRepository configRepo,
        IAccountingRepository accounts)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
    }

    public async Task<Result<CuentasParaAsiento?>> ObtenerCuentasParaCompraAsync(
        Guid tenantId,
        decimal subtotalInventario,
        decimal iva,
        CancellationToken ct)
    {
        var config = await _configRepo.GetConfiguracionEmpresaAsync(ct);
        if (config is null)
            return Result<CuentasParaAsiento?>.Success(null);

        if (config.CuentaInventarioId is null || config.CuentaProveedoresId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La configuración contable del tenant está incompleta: indique Cuenta de inventario y Cuenta de proveedores (pasivo).");

        if (iva > 0.01m && config.CuentaIvaComprasId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La compra tiene IVA pero no está configurada la cuenta de IVA compras (IVA descontable / crédito tributario).");

        var inv = await _accounts.GetByIdAsync(config.CuentaInventarioId.Value, tenantId, ct);
        var prov = await _accounts.GetByIdAsync(config.CuentaProveedoresId.Value, tenantId, ct);
        var rInv = ValidateDetail(inv, "Inventario");
        if (rInv is not null)
            return Result<CuentasParaAsiento?>.Failure(rInv);
        var rProv = ValidateDetail(prov, "Proveedores");
        if (rProv is not null)
            return Result<CuentasParaAsiento?>.Failure(rProv);

        if (config.CuentaIvaComprasId is not null)
        {
            var ivaC = await _accounts.GetByIdAsync(config.CuentaIvaComprasId.Value, tenantId, ct);
            var rIva = ValidateDetail(ivaC, "IVA compras");
            if (rIva is not null)
                return Result<CuentasParaAsiento?>.Failure(rIva);
        }

        return Result<CuentasParaAsiento?>.Success(new CuentasParaAsiento(
            CuentaDebitoPrincipal:   config.CuentaInventarioId.Value,
            CuentaCreditoPrincipal:  config.CuentaProveedoresId.Value,
            CuentaIvaDebito:         config.CuentaIvaComprasId,
            CuentaIvaCredito:        null));
    }

    public async Task<Result<CuentasParaAsiento?>> ObtenerCuentasParaVentaAsync(
        Guid tenantId,
        decimal subtotalVentas,
        decimal iva,
        CancellationToken ct)
    {
        var config = await _configRepo.GetConfiguracionEmpresaAsync(ct);
        if (config is null)
            return Result<CuentasParaAsiento?>.Success(null);

        if (config.CuentaClientesId is null || config.CuentaVentasId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La configuración contable del tenant está incompleta: indique Cuenta de clientes (activo) y Cuenta de ventas (ingreso).");

        if (iva > 0.01m && config.CuentaIvaVentasId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La venta tiene IVA pero no está configurada la cuenta de IVA ventas (IVA por pagar).");

        var cli = await _accounts.GetByIdAsync(config.CuentaClientesId.Value, tenantId, ct);
        var ven = await _accounts.GetByIdAsync(config.CuentaVentasId.Value, tenantId, ct);
        var rCli = ValidateDetail(cli, "Clientes");
        if (rCli is not null)
            return Result<CuentasParaAsiento?>.Failure(rCli);
        var rVen = ValidateDetail(ven, "Ventas");
        if (rVen is not null)
            return Result<CuentasParaAsiento?>.Failure(rVen);

        if (config.CuentaIvaVentasId is not null)
        {
            var ivaV = await _accounts.GetByIdAsync(config.CuentaIvaVentasId.Value, tenantId, ct);
            var rIva = ValidateDetail(ivaV, "IVA ventas");
            if (rIva is not null)
                return Result<CuentasParaAsiento?>.Failure(rIva);
        }

        return Result<CuentasParaAsiento?>.Success(new CuentasParaAsiento(
            CuentaDebitoPrincipal:   config.CuentaClientesId.Value,
            CuentaCreditoPrincipal:  config.CuentaVentasId.Value,
            CuentaIvaDebito:         null,
            CuentaIvaCredito:        config.CuentaIvaVentasId));
    }

    public async Task<Result<Guid?>> ObtenerCuentaParaGastoAsync(Guid tenantId, string categoriaGasto, CancellationToken ct)
    {
        var row = await _configRepo.GetGastoCategoriaByCategoriaAsync(categoriaGasto, ct);
        if (row is null)
            return Result<Guid?>.Success(null);

        var acc = await _accounts.GetByIdAsync(row.CuentaGastoId, tenantId, ct);
        var err = ValidateDetail(acc, $"Gasto categoría '{row.Categoria}'");
        if (err is not null)
            return Result<Guid?>.Failure(err);

        return Result<Guid?>.Success(row.CuentaGastoId);
    }

    public async Task<Result<Guid?>> ObtenerCuentaCajaParaGastoAsync(Guid tenantId, CancellationToken ct)
    {
        var config = await _configRepo.GetConfiguracionEmpresaAsync(ct);
        if (config is null)
            return Result<Guid?>.Success(null);

        if (config.CuentaEfectivoId is not null)
        {
            var a = await _accounts.GetByIdAsync(config.CuentaEfectivoId.Value, tenantId, ct);
            var e = ValidateDetail(a, "Caja / efectivo");
            if (e is not null)
                return Result<Guid?>.Failure(e);
            return Result<Guid?>.Success(config.CuentaEfectivoId);
        }

        if (config.CuentaBancoId is not null)
        {
            var a = await _accounts.GetByIdAsync(config.CuentaBancoId.Value, tenantId, ct);
            var err = ValidateDetail(a, "Banco");
            if (err is not null)
                return Result<Guid?>.Failure(err);
            return Result<Guid?>.Success(config.CuentaBancoId);
        }

        return Result<Guid?>.Success(null);
    }

    private static string? ValidateDetail(Account? account, string role)
    {
        if (account is null)
            return $"La cuenta configurada para {role} no existe o no pertenece al tenant.";
        if (!account.IsActive)
            return $"La cuenta de {role} está deshabilitada.";
        if (!account.AllowsMovements)
            return $"La cuenta de {role} es de agrupación (no admite movimientos). Elija una cuenta de detalle.";
        return null;
    }
}

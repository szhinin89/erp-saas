using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class CuentaContableService : ICuentaContableService
{
    private readonly IAccountingSetupRepository _configRepo;
    private readonly IAccountingRepository _accounts;

    public CuentaContableService(
        IAccountingSetupRepository configRepo,
        IAccountingRepository accounts)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
    }

    public async Task<Result<CuentasParaAsiento?>> ObtenerCuentasParaCompraAsync(
        Guid tenantId,
        decimal subtotalInventario,
        decimal  vatTotal,
        CancellationToken ct)
    {
        var config = await _configRepo.GetSetupAsync(ct);
        if (config is null)
            return Result<CuentasParaAsiento?>.Success(null);

        if (config.InventoryAccountId is null || config.SuppliersAccountId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La configuración contable del tenant está incompleta: indique Cuenta de inventario y Cuenta de proveedores (pasivo).");

        if (vatTotal > 0.01m && config.VatPurchasesAccountId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La compra tiene vatTotal pero no está configurada la cuenta de vatTotal compras (vatTotal descontable / crédito tributario).");

        var inv = await _accounts.GetByIdAsync(config.InventoryAccountId.Value, tenantId, ct);
        var prov = await _accounts.GetByIdAsync(config.SuppliersAccountId.Value, tenantId, ct);
        var rInv = ValidateDetail(inv, "Inventario");
        if (rInv is not null)
            return Result<CuentasParaAsiento?>.Failure(rInv);
        var rProv = ValidateDetail(prov, "Proveedores");
        if (rProv is not null)
            return Result<CuentasParaAsiento?>.Failure(rProv);

        if (config.VatPurchasesAccountId is not null)
        {
            var ivaC = await _accounts.GetByIdAsync(config.VatPurchasesAccountId.Value, tenantId, ct);
            var rIva = ValidateDetail(ivaC, "vatTotal compras");
            if (rIva is not null)
                return Result<CuentasParaAsiento?>.Failure(rIva);
        }

        return Result<CuentasParaAsiento?>.Success(new CuentasParaAsiento(
            CuentaDebitoPrincipal:   config.InventoryAccountId.Value,
            CuentaCreditoPrincipal:  config.SuppliersAccountId.Value,
            CuentaIvaDebito:         config.VatPurchasesAccountId,
            CuentaIvaCredito:        null));
    }

    public async Task<Result<CuentasParaAsiento?>> ObtenerCuentasParaVentaAsync(
        Guid tenantId,
        decimal subtotalVentas,
        decimal  vatTotal,
        CancellationToken ct)
    {
        var config = await _configRepo.GetSetupAsync(ct);
        if (config is null)
            return Result<CuentasParaAsiento?>.Success(null);

        if (config.CustomersAccountId is null || config.SalesAccountId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La configuración contable del tenant está incompleta: indique Cuenta de clientes (activo) y Cuenta de ventas (ingreso).");

        if (vatTotal > 0.01m && config.VatSalesAccountId is null)
            return Result<CuentasParaAsiento?>.Failure(
                "La venta tiene vatTotal pero no está configurada la cuenta de vatTotal ventas (vatTotal por pagar).");

        var cli = await _accounts.GetByIdAsync(config.CustomersAccountId.Value, tenantId, ct);
        var ven = await _accounts.GetByIdAsync(config.SalesAccountId.Value, tenantId, ct);
        var rCli = ValidateDetail(cli, "Clientes");
        if (rCli is not null)
            return Result<CuentasParaAsiento?>.Failure(rCli);
        var rVen = ValidateDetail(ven, "Ventas");
        if (rVen is not null)
            return Result<CuentasParaAsiento?>.Failure(rVen);

        if (config.VatSalesAccountId is not null)
        {
            var ivaV = await _accounts.GetByIdAsync(config.VatSalesAccountId.Value, tenantId, ct);
            var rIva = ValidateDetail(ivaV, "vatTotal ventas");
            if (rIva is not null)
                return Result<CuentasParaAsiento?>.Failure(rIva);
        }

        return Result<CuentasParaAsiento?>.Success(new CuentasParaAsiento(
            CuentaDebitoPrincipal:   config.CustomersAccountId.Value,
            CuentaCreditoPrincipal:  config.SalesAccountId.Value,
            CuentaIvaDebito:         null,
            CuentaIvaCredito:        config.VatSalesAccountId));
    }

    public async Task<Result<Guid?>> ObtenerCuentaParaGastoAsync(Guid tenantId, string   category, CancellationToken ct)
    {
        var row = await _configRepo.GetExpenseCategoryByCategoryAsync(category, ct);
        if (row is null)
            return Result<Guid?>.Success(null);

        var acc = await _accounts.GetByIdAsync(row.ExpenseAccountId, tenantId, ct);
        var err = ValidateDetail(acc, $"Gasto categoría '{row.Category}'");
        if (err is not null)
            return Result<Guid?>.Failure(err);

        return Result<Guid?>.Success(row.ExpenseAccountId);
    }

    public async Task<Result<Guid?>> ObtenerCuentaCajaParaGastoAsync(Guid tenantId, CancellationToken ct)
    {
        var config = await _configRepo.GetSetupAsync(ct);
        if (config is null)
            return Result<Guid?>.Success(null);

        if (config.CashAccountId is not null)
        {
            var a = await _accounts.GetByIdAsync(config.CashAccountId.Value, tenantId, ct);
            var e = ValidateDetail(a, "Caja / efectivo");
            if (e is not null)
                return Result<Guid?>.Failure(e);
            return Result<Guid?>.Success(config.CashAccountId);
        }

        if (config.BankAccountId is not null)
        {
            var a = await _accounts.GetByIdAsync(config.BankAccountId.Value, tenantId, ct);
            var err = ValidateDetail(a, "Banco");
            if (err is not null)
                return Result<Guid?>.Failure(err);
            return Result<Guid?>.Success(config.BankAccountId);
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


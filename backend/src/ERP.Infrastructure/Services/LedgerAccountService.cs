using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class LedgerAccountService : ILedgerAccountService
{
    private readonly IAccountingSetupRepository _configRepo;
    private readonly IAccountingRepository _accounts;

    public LedgerAccountService(
        IAccountingSetupRepository configRepo,
        IAccountingRepository accounts)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
    }

    public async Task<Result<JournalEntryAccounts?>> GetAccountsForPurchaseAsync(
        Guid subscriberId,
        decimal inventorySubtotal,
        decimal  vatTotal,
        CancellationToken ct)
    {
        var config = await _configRepo.GetSetupAsync(ct);
        if (config is null)
            return Result<JournalEntryAccounts?>.Success(null);

        if (config.InventoryAccountId is null || config.SuppliersAccountId is null)
            return Result<JournalEntryAccounts?>.Failure(
                "La configuración contable del tenant está incompleta: indique Cuenta de inventario y Cuenta de proveedores (pasivo).");

        if (vatTotal > 0.01m && config.VatPurchasesAccountId is null)
            return Result<JournalEntryAccounts?>.Failure(
                "La compra tiene vatTotal pero no está configurada la cuenta de vatTotal compras (vatTotal descontable / crédito tributario).");

        var inv = await _accounts.GetByIdAsync(config.InventoryAccountId.Value, subscriberId, ct);
        var prov = await _accounts.GetByIdAsync(config.SuppliersAccountId.Value, subscriberId, ct);
        var rInv = ValidateDetail(inv, "Inventario");
        if (rInv is not null)
            return Result<JournalEntryAccounts?>.Failure(rInv);
        var rProv = ValidateDetail(prov, "Proveedores");
        if (rProv is not null)
            return Result<JournalEntryAccounts?>.Failure(rProv);

        if (config.VatPurchasesAccountId is not null)
        {
            var ivaC = await _accounts.GetByIdAsync(config.VatPurchasesAccountId.Value, subscriberId, ct);
            var rIva = ValidateDetail(ivaC, "vatTotal compras");
            if (rIva is not null)
                return Result<JournalEntryAccounts?>.Failure(rIva);
        }

        return Result<JournalEntryAccounts?>.Success(new JournalEntryAccounts(
            MainDebitAccountId:   config.InventoryAccountId.Value,
            MainCreditAccountId:  config.SuppliersAccountId.Value,
            VatDebitAccountId:         config.VatPurchasesAccountId,
            VatCreditAccountId:        null));
    }

    public async Task<Result<JournalEntryAccounts?>> GetAccountsForSaleAsync(
        Guid subscriberId,
        decimal salesSubtotal,
        decimal  vatTotal,
        CancellationToken ct)
    {
        var config = await _configRepo.GetSetupAsync(ct);
        if (config is null)
            return Result<JournalEntryAccounts?>.Success(null);

        if (config.CustomersAccountId is null || config.SalesAccountId is null)
            return Result<JournalEntryAccounts?>.Failure(
                "La configuración contable del tenant está incompleta: indique Cuenta de clientes (activo) y Cuenta de ventas (ingreso).");

        if (vatTotal > 0.01m && config.VatSalesAccountId is null)
            return Result<JournalEntryAccounts?>.Failure(
                "La venta tiene vatTotal pero no está configurada la cuenta de vatTotal ventas (vatTotal por pagar).");

        var cli = await _accounts.GetByIdAsync(config.CustomersAccountId.Value, subscriberId, ct);
        var ven = await _accounts.GetByIdAsync(config.SalesAccountId.Value, subscriberId, ct);
        var rCli = ValidateDetail(cli, "Clientes");
        if (rCli is not null)
            return Result<JournalEntryAccounts?>.Failure(rCli);
        var rVen = ValidateDetail(ven, "Ventas");
        if (rVen is not null)
            return Result<JournalEntryAccounts?>.Failure(rVen);

        if (config.VatSalesAccountId is not null)
        {
            var ivaV = await _accounts.GetByIdAsync(config.VatSalesAccountId.Value, subscriberId, ct);
            var rIva = ValidateDetail(ivaV, "vatTotal ventas");
            if (rIva is not null)
                return Result<JournalEntryAccounts?>.Failure(rIva);
        }

        return Result<JournalEntryAccounts?>.Success(new JournalEntryAccounts(
            MainDebitAccountId:   config.CustomersAccountId.Value,
            MainCreditAccountId:  config.SalesAccountId.Value,
            VatDebitAccountId:         null,
            VatCreditAccountId:        config.VatSalesAccountId));
    }

    public async Task<Result<Guid?>> GetAccountForExpenseAsync(Guid subscriberId, string   category, CancellationToken ct)
    {
        var row = await _configRepo.GetExpenseCategoryByCategoryAsync(category, ct);
        if (row is null)
            return Result<Guid?>.Success(null);

        var acc = await _accounts.GetByIdAsync(row.ExpenseAccountId, subscriberId, ct);
        var err = ValidateDetail(acc, $"Gasto categoría '{row.Category}'");
        if (err is not null)
            return Result<Guid?>.Failure(err);

        return Result<Guid?>.Success(row.ExpenseAccountId);
    }

    public async Task<Result<Guid?>> GetCashAccountForExpenseAsync(Guid subscriberId, CancellationToken ct)
    {
        var config = await _configRepo.GetSetupAsync(ct);
        if (config is null)
            return Result<Guid?>.Success(null);

        if (config.CashAccountId is not null)
        {
            var a = await _accounts.GetByIdAsync(config.CashAccountId.Value, subscriberId, ct);
            var e = ValidateDetail(a, "Caja / efectivo");
            if (e is not null)
                return Result<Guid?>.Failure(e);
            return Result<Guid?>.Success(config.CashAccountId);
        }

        if (config.BankAccountId is not null)
        {
            var a = await _accounts.GetByIdAsync(config.BankAccountId.Value, subscriberId, ct);
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
            return $"La cuenta de {role} es de agrupación (no admite movimientos). Elija una cuenta de line.";
        return null;
    }
}


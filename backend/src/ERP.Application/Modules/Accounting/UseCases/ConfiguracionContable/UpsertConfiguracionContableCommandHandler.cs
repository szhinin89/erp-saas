using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

public sealed class UpsertConfigurationContableCommandHandler
    : IRequestHandler<UpsertConfigurationContableCommand, Result<AccountingSetupDto>>
{
    private readonly IAccountingSetupRepository _configRepo;
    private readonly IAccountingRepository      _accounts;
    private readonly ICurrentTenant             _tenant;
    private readonly ICurrentUser               _user;

    public UpsertConfigurationContableCommandHandler(
        IAccountingSetupRepository configRepo,
        IAccountingRepository accounts,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
        _tenant     = tenant;
        _user       = user;
    }

    public async Task<Result<AccountingSetupDto>> Handle(
        UpsertConfigurationContableCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var userId   = _user.UserId;

        foreach (var (role, id) in new (string, Guid?)[]
                 {
                     ("Inventario", command.InventoryAccountId),
                     ("Costo de venta", command.CostOfSalesAccountId),
                     ("Proveedores", command.SuppliersAccountId),
                     ("Ventas", command.SalesAccountId),
                     ("Clientes", command.CustomersAccountId),
                     ("IVA compras", command.VatPurchasesAccountId),
                     ("IVA ventas", command.VatSalesAccountId),
                     ("Efectivo / caja", command.CashAccountId),
                     ("Banco", command.BankAccountId),
                 })
        {
            if (id is null)
                continue;
            var err = await ValidateAccountAsync(id.Value, tenantId, role, ct);
            if (err is not null)
                return Result<AccountingSetupDto>.Failure(err);
        }

        var existing = await _configRepo.GetSetupAsync(ct);
        if (existing is null)
        {
            var created = AccountingSetup.Create(tenantId, userId);
            created.UpdateAccounts(
                command.InventoryAccountId,
                command.CostOfSalesAccountId,
                command.SuppliersAccountId,
                command.SalesAccountId,
                command.CustomersAccountId,
                command.VatPurchasesAccountId,
                command.VatSalesAccountId,
                command.CashAccountId,
                command.BankAccountId,
                userId);
            await _configRepo.AddSetupAsync(created, ct);
        }
        else
        {
            existing.UpdateAccounts(
                command.InventoryAccountId,
                command.CostOfSalesAccountId,
                command.SuppliersAccountId,
                command.SalesAccountId,
                command.CustomersAccountId,
                command.VatPurchasesAccountId,
                command.VatSalesAccountId,
                command.CashAccountId,
                command.BankAccountId,
                userId);
        }

        await _configRepo.SaveChangesAsync(ct);

        var saved = await _configRepo.GetSetupAsync(ct);
        if (saved is null)
            return Result<AccountingSetupDto>.Failure("No se pudo leer la configuración guardada.");

        return Result<AccountingSetupDto>.Success(new AccountingSetupDto(
            saved.InventoryAccountId,
            saved.CostOfSalesAccountId,
            saved.SuppliersAccountId,
            saved.SalesAccountId,
            saved.CustomersAccountId,
            saved.VatPurchasesAccountId,
            saved.VatSalesAccountId,
            saved.CashAccountId,
            saved.BankAccountId));
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

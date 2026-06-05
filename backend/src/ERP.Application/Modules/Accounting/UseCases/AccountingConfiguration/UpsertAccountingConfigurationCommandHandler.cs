using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

public sealed class UpsertConfigurationContableCommandHandler
    : IRequestHandler<UpsertAccountingConfigurationCommand, Result<AccountingSetupDto>>
{
    private readonly IAccountingSetupRepository _configRepo;
    private readonly IAccountingRepository      _accounts;
    private readonly ICurrentSubscriber         _subscriber;
    private readonly ICurrentCompany            _company;
    private readonly ICurrentUser               _user;

    public UpsertConfigurationContableCommandHandler(
        IAccountingSetupRepository configRepo,
        IAccountingRepository accounts,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
        _subscriber = subscriber;
        _company    = company;
        _user       = user;
    }

    public async Task<Result<AccountingSetupDto>> Handle(
        UpsertAccountingConfigurationCommand command,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var companyId    = _company.CompanyId;
        var userId       = _user.UserId;

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
            var err = await ValidateAccountAsync(id.Value, subscriberId, role, ct);
            if (err is not null)
                return Result<AccountingSetupDto>.Failure(err);
        }

        var existing = await _configRepo.GetSetupAsync(ct);
        if (existing is null)
        {
            var created = AccountingSetup.Create(subscriberId, companyId, userId);
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

    private async Task<string?> ValidateAccountAsync(Guid accountId, Guid subscriberId, string role, CancellationToken ct)
    {
        var a = await _accounts.GetByIdAsync(accountId, subscriberId, ct);
        if (a is null)
            return $"La cuenta de {role} no existe o no pertenece al tenant.";
        if (!a.IsActive)
            return $"La cuenta de {role} está deshabilitada.";
        if (!a.AllowsMovements)
            return $"La cuenta de {role} es de agrupación; use una cuenta de line.";
        return null;
    }
}

using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

public sealed class GetConfigurationContableQueryHandler
    : IRequestHandler<GetAccountingConfigurationQuery, Result<AccountingSetupDto?>>
{
    private readonly IAccountingSetupRepository _repo;

    public GetConfigurationContableQueryHandler(IAccountingSetupRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<AccountingSetupDto?>> Handle(
        GetAccountingConfigurationQuery request,
        CancellationToken ct)
    {
        var e = await _repo.GetSetupAsync(ct);
        if (e is null)
            return Result<AccountingSetupDto?>.Success(null);

        return Result<AccountingSetupDto?>.Success(new AccountingSetupDto(
            e.InventoryAccountId,
            e.CostOfSalesAccountId,
            e.SuppliersAccountId,
            e.SalesAccountId,
            e.CustomersAccountId,
            e.VatPurchasesAccountId,
            e.VatSalesAccountId,
            e.CashAccountId,
            e.BankAccountId));
    }
}

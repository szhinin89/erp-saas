using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

public sealed class GetConfigurationContableQueryHandler
    : IRequestHandler<GetConfigurationContableQuery, Result<AccountingSetupDto?>>
{
    private readonly IAccountingSetupRepository _repo;

    public GetConfigurationContableQueryHandler(IAccountingSetupRepository repo)
    {
        _repo = repo;
    }

    public async Task<Result<AccountingSetupDto?>> Handle(
        GetConfigurationContableQuery request,
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

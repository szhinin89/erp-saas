using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record UpsertAccountingConfigurationCommand(
    Guid? InventoryAccountId,
    Guid? CostOfSalesAccountId,
    Guid? SuppliersAccountId,
    Guid? SalesAccountId,
    Guid? CustomersAccountId,
    Guid? VatPurchasesAccountId,
    Guid? VatSalesAccountId,
    Guid? CashAccountId,
    Guid? BankAccountId
) : IRequest<Result<AccountingSetupDto>>, ICompanyScopedRequest;

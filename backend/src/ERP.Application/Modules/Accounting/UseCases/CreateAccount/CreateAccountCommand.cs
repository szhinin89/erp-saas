using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.UseCases.CreateAccount;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record CreateAccountCommand(
    string Code,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId,
    bool AllowsMovements = true
) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

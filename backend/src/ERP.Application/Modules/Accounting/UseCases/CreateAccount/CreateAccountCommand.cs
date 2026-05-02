using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Enums;

namespace ERP.Application.Accounting.UseCases.CreateAccount;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record CreateAccountCommand(
    string Code,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId
) : IRequest<Result<AccountDto>>;

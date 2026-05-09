using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Enums;

namespace ERP.Application.Accounting.UseCases.UpdateAccount;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record UpdateAccountCommand(
    Guid Id,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId
) : IRequest<Result<AccountDto>>;

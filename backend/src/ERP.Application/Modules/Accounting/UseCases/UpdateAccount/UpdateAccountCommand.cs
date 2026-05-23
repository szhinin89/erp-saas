using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Enums;

namespace ERP.Application.Modules.Accounting.UseCases.UpdateAccount;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record UpdateAccountCommand(
    Guid Id,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId,
    bool? AllowsMovements = null
) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

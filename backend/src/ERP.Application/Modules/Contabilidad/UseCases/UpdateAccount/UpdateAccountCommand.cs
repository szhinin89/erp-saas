using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Enums;

namespace ERP.Application.Modules.Contabilidad.UseCases.UpdateAccount;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record UpdateAccountCommand(
    Guid Id,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId
) : IRequest<Result<AccountDto>>;

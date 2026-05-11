using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Enums;

namespace ERP.Application.Modules.Contabilidad.UseCases.CreateAccount;

[RequireFeature(SubscriptionFeatureCodes.Accounting)]
public sealed record CreateAccountCommand(
    string Code,
    string Name,
    AccountType Type,
    AccountNature Nature,
    Guid? ParentId,
    bool AllowsMovements = true
) : IRequest<Result<AccountDto>>;

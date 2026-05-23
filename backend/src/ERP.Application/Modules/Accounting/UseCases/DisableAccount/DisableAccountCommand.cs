using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.DisableAccount;

public record DisableAccountCommand(Guid Id) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

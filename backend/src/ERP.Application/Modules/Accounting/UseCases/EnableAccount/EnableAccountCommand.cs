using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.EnableAccount;

public record EnableAccountCommand(Guid Id) : IRequest<Result<AccountDto>>;

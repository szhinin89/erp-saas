using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.EnableAccount;

public record EnableAccountCommand(Guid Id) : IRequest<Result<AccountDto>>;

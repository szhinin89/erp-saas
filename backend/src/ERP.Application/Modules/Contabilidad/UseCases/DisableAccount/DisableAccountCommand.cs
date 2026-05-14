using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.DisableAccount;

public record DisableAccountCommand(Guid Id) : IRequest<Result<AccountDto>>;

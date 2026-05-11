using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetAccountById;

public sealed record GetAccountByIdQuery(Guid Id) : IRequest<Result<AccountDto>>;

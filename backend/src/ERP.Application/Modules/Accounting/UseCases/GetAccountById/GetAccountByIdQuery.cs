using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;

namespace ERP.Application.Accounting.UseCases.GetAccountById;

public sealed record GetAccountByIdQuery(Guid Id) : IRequest<Result<AccountDto>>;

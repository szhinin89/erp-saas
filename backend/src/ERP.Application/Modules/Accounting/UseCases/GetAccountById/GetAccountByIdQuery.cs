using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.GetAccountById;

public sealed record GetAccountByIdQuery(Guid Id) : IRequest<Result<AccountDto>>, ICompanyScopedRequest;

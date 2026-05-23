using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;

namespace ERP.Application.Modules.Accounting.UseCases.GetAccounts;

public sealed record GetAccountsQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<AccountDto>>>, ICompanyScopedRequest;

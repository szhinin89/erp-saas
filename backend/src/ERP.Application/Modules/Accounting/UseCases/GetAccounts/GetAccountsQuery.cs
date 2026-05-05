using MediatR;
using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;

namespace ERP.Application.Accounting.UseCases.GetAccounts;

public sealed record GetAccountsQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<AccountDto>>>;

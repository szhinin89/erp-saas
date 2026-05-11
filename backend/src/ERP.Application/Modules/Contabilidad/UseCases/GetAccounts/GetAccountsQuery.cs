using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetAccounts;

public sealed record GetAccountsQuery(
    int PageNumber,
    int PageSize
) : IRequest<Result<PagedResult<AccountDto>>>;

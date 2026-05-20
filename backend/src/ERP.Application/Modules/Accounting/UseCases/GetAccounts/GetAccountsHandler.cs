using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using MediatR;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.GetAccounts;

public class GetAccountsHandler : IRequestHandler<GetAccountsQuery, Result<PagedResult<AccountDto>>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;

    public GetAccountsHandler(IAccountingRepository repository, ICurrentSubscriber currentSubscriber)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
    }

    public Task<Result<PagedResult<AccountDto>>> HandleAsync(int pageNumber, int pageSize, CancellationToken ct = default)
        => Handle(new GetAccountsQuery(pageNumber, pageSize), ct);

    public async Task<Result<PagedResult<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var (accounts, totalCount) = await _repository.GetAccountsPageAsync(
            subscriberId,
            request.PageNumber,
            request.PageSize,
            ct);

        var dtos = accounts.Select(a => new AccountDto(
            a.Id, a.Code.Value, a.Name,
            a.Type.ToString(), a.Nature.ToString(),
            a.IsActive, a.AllowsMovements, a.ParentId, a.CreatedAt))
            .ToList();

        return Result<PagedResult<AccountDto>>.Success(new PagedResult<AccountDto>(
            Items: dtos,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            TotalCount: totalCount));
    }
}

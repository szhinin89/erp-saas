using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using MediatR;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.GetAccounts;

public class GetAccountsHandler : IRequestHandler<GetAccountsQuery, Result<PagedResult<AccountDto>>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetAccountsHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public Task<Result<PagedResult<AccountDto>>> HandleAsync(int pageNumber, int pageSize, CancellationToken ct = default)
        => Handle(new GetAccountsQuery(pageNumber, pageSize), ct);

    public async Task<Result<PagedResult<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var (accounts, totalCount) = await _repository.GetAccountsPageAsync(
            tenantId,
            request.PageNumber,
            request.PageSize,
            ct);

        var dtos = accounts.Select(a => new AccountDto(
            a.Id, a.Code.Value, a.Name,
            a.Type.ToString(), a.Nature.ToString(),
            a.IsActive, a.ParentId, a.CreatedAt))
            .ToList();

        return Result<PagedResult<AccountDto>>.Success(new PagedResult<AccountDto>(
            Items: dtos,
            PageNumber: request.PageNumber,
            PageSize: request.PageSize,
            TotalCount: totalCount));
    }
}

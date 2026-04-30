using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Interfaces;

namespace ERP.Application.Accounting.UseCases.GetAccounts;

public class GetAccountsHandler
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetAccountsHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<PagedResult<AccountDto>>> HandleAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var (accounts, totalCount) = await _repository.GetAccountsPageAsync(tenantId, pageNumber, pageSize, ct);

        var dtos = accounts.Select(a => new AccountDto(
            a.Id, a.Code.Value, a.Name,
            a.Type.ToString(), a.Nature.ToString(),
            a.IsActive, a.ParentId, a.CreatedAt))
            .ToList();

        return Result<PagedResult<AccountDto>>.Success(new PagedResult<AccountDto>(
            Items: dtos,
            PageNumber: pageNumber,
            PageSize: pageSize,
            TotalCount: totalCount));
    }
}

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

    public async Task<Result<IReadOnlyList<AccountDto>>> HandleAsync(CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var accounts = await _repository.GetAllByTenantAsync(tenantId, ct);

        var dtos = accounts.Select(a => new AccountDto(
            a.Id, a.Code.Value, a.Name,
            a.Type.ToString(), a.Nature.ToString(),
            a.IsActive, a.ParentId, a.CreatedAt))
            .ToList();

        return Result<IReadOnlyList<AccountDto>>.Success(dtos);
    }
}

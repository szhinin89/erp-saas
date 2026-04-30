using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Interfaces;

namespace ERP.Application.Accounting.UseCases.GetAccountById;

public class GetAccountByIdHandler
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;

    public GetAccountByIdHandler(IAccountingRepository repository, ICurrentTenant currentTenant)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
    }

    public async Task<Result<AccountDto>> HandleAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var account  = await _repository.GetByIdAsync(id, tenantId, ct);

        if (account is null)
            return Result<AccountDto>.Failure("Cuenta no encontrada.");

        return Result<AccountDto>.Success(new AccountDto(
            account.Id, account.Code.Value, account.Name,
            account.Type.ToString(), account.Nature.ToString(),
            account.IsActive, account.ParentId, account.CreatedAt));
    }
}

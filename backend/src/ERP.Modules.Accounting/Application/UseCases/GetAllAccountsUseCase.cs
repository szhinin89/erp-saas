using Modules.Accounting.Application.DTOs;
using Modules.Accounting.Application.Interfaces;

namespace Modules.Accounting.Application.UseCases;

public class GetAllAccountsUseCase(IAccountRepository repo)
{
    public async Task<IReadOnlyList<AccountDto>> ExecuteAsync(Guid tenantId, CancellationToken ct = default)
    {
        var accounts = await repo.GetAllByTenantAsync(tenantId, ct);
        return accounts.Select(a => new AccountDto
        {
            Id = a.Id,
            TenantId = a.TenantId,
            Code = a.Code,
            Name = a.Name,
            Type = a.Type,
            Nature = a.Nature,
            ParentId = a.ParentId,
            Level = a.Level,
            AllowsMovement = a.AllowsMovement,
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt
        }).ToList();
    }
}

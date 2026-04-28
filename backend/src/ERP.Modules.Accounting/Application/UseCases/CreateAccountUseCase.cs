using Modules.Accounting.Application.DTOs;
using Modules.Accounting.Application.Interfaces;
using Modules.Accounting.Domain.Entities;

namespace Modules.Accounting.Application.UseCases;

public class CreateAccountUseCase(IAccountRepository repo)
{
    public async Task<AccountDto> ExecuteAsync(Guid tenantId, CreateAccountRequest request, CancellationToken ct = default)
    {
        if (await repo.ExistsAsync(request.Code, tenantId, ct))
            throw new InvalidOperationException($"Ya existe una cuenta con el código {request.Code}");

        var account = Account.Create(
            tenantId,
            request.Code,
            request.Name,
            request.Type,
            request.Nature,
            request.AllowsMovement,
            request.ParentId,
            request.Level);

        await repo.AddAsync(account, ct);
        await repo.SaveChangesAsync(ct);

        return new AccountDto
        {
            Id = account.Id,
            TenantId = account.TenantId,
            Code = account.Code,
            Name = account.Name,
            Type = account.Type,
            Nature = account.Nature,
            ParentId = account.ParentId,
            Level = account.Level,
            AllowsMovement = account.AllowsMovement,
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt
        };
    }
}

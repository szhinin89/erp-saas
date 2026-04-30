using ERP.Application.Common;
using ERP.Application.Accounting.DTOs;
using ERP.Domain.Accounting.Entities;
using ERP.Domain.Accounting.Interfaces;

namespace ERP.Application.Accounting.UseCases.CreateAccount;

public class CreateAccountHandler
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateAccountHandler(
        IAccountingRepository repository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<AccountDto>> HandleAsync(
        CreateAccountCommand command,
        CancellationToken ct = default)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var exists = await _repository.ExistsAsync(command.Code, tenantId, ct);
        if (exists)
            return Result<AccountDto>.Failure($"Ya existe una cuenta con el codigo '{command.Code}'.");

        var account = Account.Create(
            tenantId,
            command.Code,
            command.Name,
            command.Type,
            command.Nature,
            userId,
            command.ParentId);

        await _repository.AddAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<AccountDto>.Success(new AccountDto(
            account.Id,
            account.Code.Value,
            account.Name,
            account.Type.ToString(),
            account.Nature.ToString(),
            account.IsActive,
            account.ParentId,
            account.CreatedAt));
    }
}

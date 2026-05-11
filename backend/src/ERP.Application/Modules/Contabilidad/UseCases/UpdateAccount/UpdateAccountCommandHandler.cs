using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.UpdateAccount;

public sealed class UpdateAccountCommandHandler : IRequestHandler<UpdateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public UpdateAccountCommandHandler(
        IAccountingRepository repository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<AccountDto>> Handle(UpdateAccountCommand command, CancellationToken ct)
    {
        var tenantId = _currentTenant.TenantId;
        var userId   = _currentUser.UserId;

        var account = await _repository.GetByIdAsync(command.Id, tenantId, ct);
        if (account is null)
            return Result<AccountDto>.Failure("Cuenta no encontrada.");

        account.Update(command.Name, command.Type, command.Nature, userId, command.ParentId);

        await _repository.UpdateAsync(account, ct);
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

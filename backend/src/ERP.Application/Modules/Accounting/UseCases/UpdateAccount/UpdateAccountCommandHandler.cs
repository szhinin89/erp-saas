using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.UpdateAccount;

public sealed class UpdateAccountCommandHandler : IRequestHandler<UpdateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public UpdateAccountCommandHandler(
        IAccountingRepository repository,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<AccountDto>> Handle(UpdateAccountCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var account = await _repository.GetByIdAsync(command.Id, subscriberId, ct);
        if (account is null)
            return Result<AccountDto>.Failure("Cuenta no encontrada.");

        account.Update(command.Name, command.Type, command.Nature, userId, command.ParentId, command.AllowsMovements);

        await _repository.UpdateAsync(account, ct);
        await _repository.SaveChangesAsync(ct);

        return Result<AccountDto>.Success(new AccountDto(
            account.Id,
            account.Code.Value,
            account.Name,
            account.Type.ToString(),
            account.Nature.ToString(),
            account.IsActive,
            account.AllowsMovements,
            account.ParentId,
            account.CreatedAt));
    }
}

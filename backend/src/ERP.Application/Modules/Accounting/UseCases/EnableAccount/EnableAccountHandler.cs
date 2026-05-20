using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Audit.Entities;
using ERP.Domain.Audit.Interfaces;
using ERP.Domain.Modules.Accounting.Interfaces;

using MediatR;

namespace ERP.Application.Modules.Accounting.UseCases.EnableAccount;

public class EnableAccountHandler : IRequestHandler<EnableAccountCommand, Result<AccountDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly IUserActivityRepository _activity;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentUser _currentUser;

    public EnableAccountHandler(
        IAccountingRepository repository,
        IUserActivityRepository activity,
        ICurrentSubscriber currentSubscriber,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _activity      = activity;
        _currentSubscriber = currentSubscriber;
        _currentUser   = currentUser;
    }

    public async Task<Result<AccountDto>> Handle(EnableAccountCommand command, CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var userId   = _currentUser.UserId;

        var account = await _repository.GetByIdAsync(command.Id, subscriberId, ct);
        if (account is null)
            return Result<AccountDto>.Failure("Cuenta no encontrada.");

        if (account.IsActive)
            return Result<AccountDto>.Failure("La cuenta ya está activa.");

        account.Enable(userId);

        await _activity.AddAsync(UserActivity.Create(
            subscriberId,
            userId,
            _currentUser.Email,
            _currentUser.FullName,
            module: "accounting",
            action: "account.enable",
            entityType: "Account",
            entityId: account.Id,
            description: $"{account.Code.Value} — {account.Name}"), ct);

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

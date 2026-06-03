using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.CreateAccount;

public sealed class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentSubscriber _currentSubscriber;
    private readonly ICurrentCompany _currentCompany;
    private readonly ICurrentUser _currentUser;

    public CreateAccountCommandHandler(
        IAccountingRepository repository,
        ICurrentSubscriber currentSubscriber,
        ICurrentCompany currentCompany,
        ICurrentUser currentUser)
    {
        _repository        = repository;
        _currentSubscriber = currentSubscriber;
        _currentCompany    = currentCompany;
        _currentUser       = currentUser;
    }

    public async Task<Result<AccountDto>> Handle(
        CreateAccountCommand command,
        CancellationToken ct)
    {
        var subscriberId = _currentSubscriber.SubscriberId;
        var companyId    = _currentCompany.CompanyId;
        var userId       = _currentUser.UserId;

        var exists = await _repository.ExistsAsync(command.Code, subscriberId, ct);
        if (exists)
            return Result<AccountDto>.Failure($"Ya existe una cuenta con el codigo '{command.Code}'.");

        var account = Account.Create(
            subscriberId,
            companyId,
            command.Code,
            command.Name,
            command.Type,
            command.Nature,
            userId,
            command.ParentId,
            command.AllowsMovements);

        await _repository.AddAsync(account, ct);
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

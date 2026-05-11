using MediatR;
using ERP.Application.Common;
using ERP.Application.Modules.Contabilidad.DTOs;
using ERP.Domain.Modules.Contabilidad.Entities;
using ERP.Domain.Modules.Contabilidad.Interfaces;

namespace ERP.Application.Modules.Contabilidad.UseCases.CreateAccount;

public sealed class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<AccountDto>>
{
    private readonly IAccountingRepository _repository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;

    public CreateAccountCommandHandler(
        IAccountingRepository repository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser)
    {
        _repository    = repository;
        _currentTenant = currentTenant;
        _currentUser   = currentUser;
    }

    public async Task<Result<AccountDto>> Handle(
        CreateAccountCommand command,
        CancellationToken ct)
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

using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.AccountingConfiguration;

public sealed class CreateExpenseCategoryCommandHandler
    : IRequestHandler<CreateExpenseCategoryCommand, Result<ExpenseCategoryDto>>
{
    private readonly IAccountingSetupRepository _configRepo;
    private readonly IAccountingRepository      _accounts;
    private readonly ICurrentSubscriber             _subscriber;
    private readonly ICurrentUser               _user;

    public CreateExpenseCategoryCommandHandler(
        IAccountingSetupRepository configRepo,
        IAccountingRepository accounts,
        ICurrentSubscriber subscriber,
        ICurrentUser user)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
        _subscriber = subscriber;
        _user       = user;
    }

    public async Task<Result<ExpenseCategoryDto>> Handle(
        CreateExpenseCategoryCommand command,
        CancellationToken ct)
    {
        var subscriberId = _subscriber.SubscriberId;
        var dup      = await _configRepo.GetExpenseCategoryByCategoryAsync(command.Category, ct);
        if (dup is not null)
            return Result<ExpenseCategoryDto>.Failure("Ya existe un mapeo para esa categoría.");

        var acc = await _accounts.GetByIdAsync(command.ExpenseAccountId, subscriberId, ct);
        if (acc is null)
            return Result<ExpenseCategoryDto>.Failure("La cuenta de gasto no existe o no pertenece al tenant.");
        if (!acc.IsActive)
            return Result<ExpenseCategoryDto>.Failure("La cuenta de gasto está deshabilitada.");
        if (!acc.AllowsMovements)
            return Result<ExpenseCategoryDto>.Failure("La cuenta es de agrupación; use una cuenta de detalle.");

        var entity = ExpenseCategory.Create(subscriberId, command.Category, command.ExpenseAccountId, _user.UserId);
        await _configRepo.AddExpenseCategoryAsync(entity, ct);
        await _configRepo.SaveChangesAsync(ct);

        return Result<ExpenseCategoryDto>.Success(
            new ExpenseCategoryDto(entity.Id, entity.Category, entity.ExpenseAccountId));
    }
}

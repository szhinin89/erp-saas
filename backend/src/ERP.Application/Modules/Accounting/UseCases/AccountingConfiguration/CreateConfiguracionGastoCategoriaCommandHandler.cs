using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.Accounting.DTOs;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Application.Modules.Accounting.UseCases.ConfiguracionContable;

public sealed class CreateExpenseCategoryCommandHandler
    : IRequestHandler<CreateExpenseCategoryCommand, Result<ExpenseCategoryDto>>
{
    private readonly IAccountingSetupRepository _configRepo;
    private readonly IAccountingRepository      _accounts;
    private readonly ICurrentTenant             _tenant;
    private readonly ICurrentUser               _user;

    public CreateExpenseCategoryCommandHandler(
        IAccountingSetupRepository configRepo,
        IAccountingRepository accounts,
        ICurrentTenant tenant,
        ICurrentUser user)
    {
        _configRepo = configRepo;
        _accounts   = accounts;
        _tenant     = tenant;
        _user       = user;
    }

    public async Task<Result<ExpenseCategoryDto>> Handle(
        CreateExpenseCategoryCommand command,
        CancellationToken ct)
    {
        var tenantId = _tenant.TenantId;
        var dup      = await _configRepo.GetExpenseCategoryByCategoryAsync(command.Category, ct);
        if (dup is not null)
            return Result<ExpenseCategoryDto>.Failure("Ya existe un mapeo para esa categoría.");

        var acc = await _accounts.GetByIdAsync(command.ExpenseAccountId, tenantId, ct);
        if (acc is null)
            return Result<ExpenseCategoryDto>.Failure("La cuenta de gasto no existe o no pertenece al tenant.");
        if (!acc.IsActive)
            return Result<ExpenseCategoryDto>.Failure("La cuenta de gasto está deshabilitada.");
        if (!acc.AllowsMovements)
            return Result<ExpenseCategoryDto>.Failure("La cuenta es de agrupación; use una cuenta de detalle.");

        var entity = ExpenseCategory.Create(tenantId, command.Category, command.ExpenseAccountId, _user.UserId);
        await _configRepo.AddExpenseCategoryAsync(entity, ct);
        await _configRepo.SaveChangesAsync(ct);

        return Result<ExpenseCategoryDto>.Success(
            new ExpenseCategoryDto(entity.Id, entity.Category, entity.ExpenseAccountId));
    }
}

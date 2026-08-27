using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Expenses.DTOs;
using ERP.Application.Modules.Expenses.Exceptions;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using FluentValidation;
using MediatR;

namespace ERP.Application.Modules.Expenses.UseCases.Documents;

public sealed record ConfirmExpenseDocumentCommand(Guid Id)
    : IRequest<Result<ExpenseDocumentDetailDto>>,
        IBranchScopedRequest;

public sealed class ConfirmExpenseDocumentValidator : AbstractValidator<ConfirmExpenseDocumentCommand>
{
    public ConfirmExpenseDocumentValidator() => RuleFor(x => x.Id).NotEmpty();
}

public sealed class ConfirmExpenseDocumentHandler
    : IRequestHandler<ConfirmExpenseDocumentCommand, Result<ExpenseDocumentDetailDto>>
{
    private readonly IExpenseDocumentRepository _repo;
    private readonly IExpenseCategoryRepository _categories;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentCompany _company;
    private readonly ICurrentBranch _branch;
    private readonly ICurrentUser _user;

    public ConfirmExpenseDocumentHandler(
        IExpenseDocumentRepository repo,
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentBranch branch,
        ICurrentUser user
    )
    {
        _repo = repo;
        _categories = categories;
        _accounts = accounts;
        _tenant = tenant;
        _company = company;
        _branch = branch;
        _user = user;
    }

    public async Task<Result<ExpenseDocumentDetailDto>> Handle(
        ConfirmExpenseDocumentCommand cmd,
        CancellationToken ct
    )
    {
        var document = await _repo.GetByIdAsync(_tenant.TenantId, cmd.Id, ct);
        if (document is null || document.BranchId != _branch.BranchId)
            return Result<ExpenseDocumentDetailDto>.NotFound("Gasto no encontrado.");
        if (document.Status != ExpenseStatus.Draft)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "Solo se pueden confirmar gastos en estado borrador."
            );
        if (document.SupplierId == Guid.Empty)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener un proveedor para confirmarse."
            );
        if (document.BranchId == Guid.Empty)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener una sucursal valida para confirmarse."
            );
        if (document.Lines.Count == 0)
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(
                "El gasto debe tener al menos una linea para confirmarse."
            );

        var snapshots = new Dictionary<Guid, (Guid AccountId, string? Code, string? Name)>();
        foreach (var line in document.Lines)
        {
            var resolution = await ExpenseConfirmRules.ResolveLineAccountAsync(
                _categories,
                _accounts,
                _tenant.TenantId,
                _company.CompanyId,
                line,
                ct
            );
            if (resolution.Error is not null)
                return resolution.Error.ToResult<ExpenseDocumentDetailDto>();

            snapshots[line.Id] = (
                resolution.Account!.Id,
                resolution.Account.Code.Value,
                resolution.Account.Name
            );
        }

        try
        {
            document.Confirm(snapshots, _user.UserId);
        }
        catch (ArgumentException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message);
        }

        try
        {
            // EXPENSES-CONFIRM-07: a diferencia de Purchases/Sales, el posting de Gastos es
            // estricto — ExpenseDocumentConfirmedPostingTranslator lanza ExpensePostingFailedException
            // (en vez de solo loguear un warning) si IPostingEngine.PostAsync falla. La excepción se
            // propaga desde el Publish() interno de ErpDbContext.SaveChangesAsync, que hace rollback
            // completo de la transacción ANTES de este catch — el documento queda en Draft en BD,
            // nada de lo mutado en memoria (Confirm() de arriba) llegó a persistirse.
            await _repo.SaveChangesAsync(ct);
        }
        catch (ExpensePostingFailedException ex)
        {
            return Result<ExpenseDocumentDetailDto>.ValidationFailure(ex.Message, ex.Code);
        }

        return Result<ExpenseDocumentDetailDto>.Success(ExpenseDocumentMapper.ToDetail(document));
    }
}

file sealed record ExpenseConfirmError(string Message, string Code)
{
    public Result<T> ToResult<T>() =>
        Code == ApiResponseCodes.Common.NotFound
            ? Result<T>.NotFound(Message)
            : Result<T>.ValidationFailure(Message);
}

file sealed record LineAccountResolution(Account? Account, ExpenseConfirmError? Error);

/// <summary>
/// EXPENSES-CONFIRM-07 — re-valida, al confirmar, exactamente las mismas reglas que
/// <c>ExpenseDraftRules.BuildLinesAsync</c> exige al crear/editar el borrador (subcategoría activa,
/// nivel Subcategory, misma empresa, cuenta contable activa/postable/tipo Gasto/misma empresa).
/// Se revalida aquí porque ambas pudieron cambiar entre la creación del borrador y la confirmación
/// (la subcategoría puede desactivarse o cambiar de cuenta; la cuenta puede desactivarse o perder
/// AllowsPosting) — el snapshot tomado al crear la línea puede estar obsoleto.
/// </summary>
file static class ExpenseConfirmRules
{
    public static async Task<LineAccountResolution> ResolveLineAccountAsync(
        IExpenseCategoryRepository categories,
        IAccountRepository accounts,
        Guid tenantId,
        Guid companyId,
        ExpenseLine line,
        CancellationToken ct
    )
    {
        if (line.ExpenseSubcategoryId == Guid.Empty)
            return new LineAccountResolution(
                null,
                Validation("Cada linea debe tener una subcategoria de gasto.")
            );

        var category = await categories.GetByIdAsync(tenantId, line.ExpenseSubcategoryId, ct);
        if (category is null || category.CompanyId != companyId)
            return new LineAccountResolution(
                null,
                new ExpenseConfirmError(
                    "La subcategoria de gasto no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!category.IsActive)
            return new LineAccountResolution(
                null,
                Validation($"La subcategoria '{category.Name}' esta inactiva.")
            );
        if (category.Level != ExpenseCategoryNodeLevel.Subcategory)
            return new LineAccountResolution(
                null,
                Validation("Cada linea debe apuntar a una subcategoria de gasto.")
            );
        if (!category.AccountingAccountId.HasValue)
            return new LineAccountResolution(
                null,
                Validation(
                    $"La subcategoria '{category.Name}' no tiene cuenta contable configurada."
                )
            );

        var account = await accounts.GetByIdAsync(
            tenantId,
            companyId,
            category.AccountingAccountId.Value,
            ct
        );
        if (account is null)
            return new LineAccountResolution(
                null,
                new ExpenseConfirmError(
                    $"La cuenta contable de la subcategoria '{category.Name}' no existe.",
                    ApiResponseCodes.Common.NotFound
                )
            );
        if (!account.IsActive)
            return new LineAccountResolution(
                null,
                Validation($"La cuenta contable de la subcategoria '{category.Name}' esta inactiva.")
            );
        if (!account.AllowsPosting)
            return new LineAccountResolution(
                null,
                Validation(
                    $"La cuenta contable de la subcategoria '{category.Name}' no permite contabilizacion."
                )
            );
        if (account.AccountType != AccountType.Expense)
            return new LineAccountResolution(
                null,
                Validation($"La cuenta contable de la subcategoria '{category.Name}' debe ser de tipo gasto.")
            );

        return new LineAccountResolution(account, null);
    }

    private static ExpenseConfirmError Validation(string message) =>
        new(message, ApiResponseCodes.Common.ValidationError);
}

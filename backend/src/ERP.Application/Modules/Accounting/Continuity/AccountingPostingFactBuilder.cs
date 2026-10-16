using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — reconstruye el <see cref="PostingFact"/> de una obligación a partir
/// de su payload persistido (importes del origen) y de la configuración contable VIGENTE al
/// momento del intento (accounting.md §6). Nunca lanza por datos o configuración: cualquier
/// carencia es un resultado fallido que el intento registra como Failed.
/// </summary>
public sealed class AccountingPostingFactBuilder
{
    public const string PayloadUnsupportedCode = "ACCOUNTING_PAYLOAD_UNSUPPORTED";
    public const string ExpenseAccountNotConfiguredCode = "EXPENSE_ACCOUNT_NOT_CONFIGURED";
    public const string ExpenseAccountInvalidCode = "EXPENSE_ACCOUNT_INVALID";

    private readonly IExpenseCategoryRepository _expenseCategories;
    private readonly IAccountRepository _accounts;

    public AccountingPostingFactBuilder(
        IExpenseCategoryRepository expenseCategories,
        IAccountRepository accounts
    )
    {
        _expenseCategories = expenseCategories;
        _accounts = accounts;
    }

    public async Task<Result<PostingFact>> BuildAsync(AccountingPosting posting, CancellationToken ct)
    {
        var payload = AccountingPostingPayload.Deserialize(posting.PayloadVersion, posting.Payload);
        if (payload is null)
            return Result<PostingFact>.ValidationFailure(
                $"Versión de datos contables no soportada ({posting.PayloadVersion}).",
                PayloadUnsupportedCode
            );

        var subtotal = payload.Subtotal;
        IReadOnlyCollection<PostingAllocation>? allocations = null;
        if (payload.ExpenseLines is { } lines)
        {
            var resolved = await ResolveExpenseAllocationsAsync(posting, lines, ct);
            if (!resolved.IsSuccess)
                return Result<PostingFact>.ValidationFailure(resolved.Error!, resolved.Code);
            allocations = resolved.Value!;
            subtotal = allocations.Sum(a => a.Amount);
        }

        return Result<PostingFact>.Success(
            new PostingFact(
                posting.TenantId,
                posting.CompanyId,
                posting.SourceModule,
                posting.FactType,
                posting.SourceEventId,
                posting.EntryDate,
                subtotal,
                payload.TotalVat,
                payload.TotalIce,
                payload.TotalDiscount,
                payload.GrandTotal,
                TotalIrbpnr: payload.TotalIrbpnr,
                Allocations: allocations,
                RetainedAmount: payload.RetainedAmount,
                RetainedVatAmount: payload.RetainedVatAmount,
                RetainedIncomeAmount: payload.RetainedIncomeAmount
            )
        );
    }

    /// <summary>
    /// Una allocation Debe por línea de gasto con importe, a la cuenta configurada HOY en su
    /// subcategoría. Una línea de importe cero no tiene efecto contable y no genera allocation.
    /// </summary>
    private async Task<Result<IReadOnlyCollection<PostingAllocation>>> ResolveExpenseAllocationsAsync(
        AccountingPosting posting,
        IReadOnlyList<AccountingPostingExpenseLine> lines,
        CancellationToken ct
    )
    {
        var allocations = new List<PostingAllocation>();
        foreach (var line in lines.Where(l => l.Amount > 0m))
        {
            var category = await _expenseCategories.GetByIdAsync(
                posting.TenantId,
                line.ExpenseSubcategoryId,
                ct
            );
            if (category is null || category.CompanyId != posting.CompanyId)
                return Failure($"La subcategoría de gasto de la línea '{line.Description}' no existe.", ExpenseAccountNotConfiguredCode);
            if (category.AccountingAccountId is not { } accountId)
                return Failure($"La subcategoría '{category.Name}' no tiene cuenta contable configurada.", ExpenseAccountNotConfiguredCode);

            var account = await _accounts.GetByIdAsync(posting.TenantId, posting.CompanyId, accountId, ct);
            if (account is null)
                return Failure($"La cuenta contable de la subcategoría '{category.Name}' no existe.", ExpenseAccountInvalidCode);
            if (!account.IsActive || !account.AllowsPosting)
                return Failure(
                    $"La cuenta contable de la subcategoría '{category.Name}' está inactiva o no permite contabilización.",
                    ExpenseAccountInvalidCode
                );
            if (account.AccountType != AccountType.Expense)
                return Failure(
                    $"La cuenta contable de la subcategoría '{category.Name}' debe ser de tipo gasto.",
                    ExpenseAccountInvalidCode
                );

            allocations.Add(
                new PostingAllocation(
                    account.Id,
                    line.Amount,
                    AccountNature.Debit,
                    line.Description,
                    line.ExpenseLineId
                )
            );
        }

        return Result<IReadOnlyCollection<PostingAllocation>>.Success(allocations);

        static Result<IReadOnlyCollection<PostingAllocation>> Failure(string message, string code) =>
            Result<IReadOnlyCollection<PostingAllocation>>.ValidationFailure(message, code);
    }
}

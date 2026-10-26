using ERP.Application.Common;
using ERP.Application.Modules.Accounting.Posting;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.Interfaces;
using ERP.Domain.Modules.Caja.Interfaces;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Domain.Modules.Finance.Interfaces;
using ERP.Domain.Modules.Inventory.Interfaces;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Application.Modules.Accounting.Continuity;

/// <summary>
/// ACCOUNTING-CONTINUITY-01 — reconstruye el <see cref="PostingFact"/> de una obligación a partir
/// de su payload persistido (importes del origen) y de la configuración contable VIGENTE al
/// momento del intento (accounting.md §6). Nunca lanza por datos o configuración: cualquier
/// carencia es un resultado fallido que el intento registra como Failed.
/// </summary>
/// <remarks>
/// ACCOUNTING-CONTINUITY-04 — también resuelve aquí (y no en el núcleo operativo) las cuentas de
/// los destinos financieros (caja, cuenta bancaria, medio tarjeta/cheque) y exige las líneas de
/// regla que un importe no nulo necesita (IRBPNR, anticipo a proveedor).
/// </remarks>
public sealed class AccountingPostingFactBuilder
{
    public const string PayloadUnsupportedCode = "ACCOUNTING_PAYLOAD_UNSUPPORTED";
    public const string ExpenseAccountNotConfiguredCode = "EXPENSE_ACCOUNT_NOT_CONFIGURED";
    public const string ExpenseAccountInvalidCode = "EXPENSE_ACCOUNT_INVALID";
    public const string DestinationNotFoundCode = "FINANCIAL_DESTINATION_NOT_FOUND";
    public const string DestinationAccountNotConfiguredCode = "FINANCIAL_DESTINATION_ACCOUNT_NOT_CONFIGURED";
    public const string RuleLineNotConfiguredCode = "POSTING_RULE_LINE_NOT_CONFIGURED";
    public const string AdjustmentAccountNotConfiguredCode = "INVENTORY_ADJUSTMENT_ACCOUNT_NOT_CONFIGURED";
    public const string AdjustmentAccountInvalidCode = "INVENTORY_ADJUSTMENT_ACCOUNT_INVALID";
    public const string CashReasonAccountNotConfiguredCode = "CASH_MOVEMENT_REASON_ACCOUNT_NOT_CONFIGURED";
    public const string CashReasonAccountInvalidCode = "CASH_MOVEMENT_REASON_ACCOUNT_INVALID";

    private readonly IExpenseCategoryRepository _expenseCategories;
    private readonly IAccountRepository _accounts;
    private readonly ICashRegisterRepository _cashRegisters;
    private readonly ICompanyBankAccountRepository _bankAccounts;
    private readonly IPaymentMethodAccountRepository _paymentMethodAccounts;
    private readonly IPostingRuleRepository _rules;
    private readonly IInventoryAdjustmentReasonRepository _adjustmentReasons;
    private readonly IInventoryAdjustmentReasonAccountRepository _adjustmentReasonAccounts;
    private readonly ICashMovementReasonRepository _cashReasons;

    public AccountingPostingFactBuilder(
        IExpenseCategoryRepository expenseCategories,
        IAccountRepository accounts,
        ICashRegisterRepository cashRegisters,
        ICompanyBankAccountRepository bankAccounts,
        IPaymentMethodAccountRepository paymentMethodAccounts,
        IPostingRuleRepository rules,
        IInventoryAdjustmentReasonRepository adjustmentReasons,
        IInventoryAdjustmentReasonAccountRepository adjustmentReasonAccounts,
        ICashMovementReasonRepository cashReasons
    )
    {
        _cashReasons = cashReasons;
        _adjustmentReasons = adjustmentReasons;
        _adjustmentReasonAccounts = adjustmentReasonAccounts;
        _expenseCategories = expenseCategories;
        _accounts = accounts;
        _cashRegisters = cashRegisters;
        _bankAccounts = bankAccounts;
        _paymentMethodAccounts = paymentMethodAccounts;
        _rules = rules;
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
        var allocations = new List<PostingAllocation>();
        if (payload.ExpenseLines is { } lines)
        {
            var resolved = await ResolveExpenseAllocationsAsync(posting, lines, ct);
            if (!resolved.IsSuccess)
                return Result<PostingFact>.ValidationFailure(resolved.Error!, resolved.Code);
            allocations.AddRange(resolved.Value!);
            subtotal = resolved.Value!.Sum(a => a.Amount);
        }

        PostingAmountKind? overrideKind = null;
        AccountNature? overrideNature = null;
        Guid? overrideAccountId = null;
        var destinationAllocations = new Dictionary<(Guid Account, AccountNature Nature), (decimal Amount, string? Description)>();
        foreach (var destination in payload.Destinations ?? [])
        {
            if (destination.Amount <= 0m)
                continue;
            var account = await ResolveDestinationAccountAsync(posting, destination, ct);
            if (!account.IsSuccess)
                return Result<PostingFact>.ValidationFailure(account.Error!, account.Code);

            if (destination.OverridesAmountKind is { } kind)
            {
                overrideKind = kind;
                overrideNature = destination.Nature;
                overrideAccountId = account.Value;
                continue;
            }

            // Mismo criterio previo de Ventas: una línea por cuenta y naturaleza.
            var key = (account.Value, destination.Nature);
            destinationAllocations[key] = destinationAllocations.TryGetValue(key, out var current)
                ? (current.Amount + destination.Amount, current.Description == destination.Description ? current.Description : null)
                : (destination.Amount, destination.Description);
        }
        allocations.AddRange(
            destinationAllocations.Select(d => new PostingAllocation(d.Key.Account, d.Value.Amount, d.Key.Nature, d.Value.Description))
        );

        var requiredLines = await EnsureRequiredRuleLinesAsync(posting, payload, ct);
        if (!requiredLines.IsSuccess)
            return Result<PostingFact>.ValidationFailure(requiredLines.Error!, requiredLines.Code);

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
                AppliedToPayableAmount: payload.AppliedToPayableAmount,
                SupplierCreditAmount: payload.SupplierCreditAmount,
                CostVarianceDebitAmount: payload.CostVarianceDebitAmount,
                CostVarianceCreditAmount: payload.CostVarianceCreditAmount,
                HistoricalCostTotal: payload.HistoricalCostTotal,
                TotalIrbpnr: payload.TotalIrbpnr,
                OverrideAmountKind: overrideKind,
                OverrideAccountNature: overrideNature,
                OverrideAccountId: overrideAccountId,
                Allocations: allocations.Count > 0 ? allocations : null,
                RetainedAmount: payload.RetainedAmount,
                RetainedVatAmount: payload.RetainedVatAmount,
                RetainedIncomeAmount: payload.RetainedIncomeAmount,
                CashApplied: payload.CashApplied,
                PendingBalance: payload.PendingBalance
            )
        );
    }

    /// <summary>
    /// Cuenta vigente del destino: caja → su cuenta; banco → su cuenta; tarjeta/cheque → la
    /// configuración del medio por Company. Sin fallback. La validez de la cuenta (activa,
    /// postable, misma Company) la exige después el motor.
    /// </summary>
    private async Task<Result<Guid>> ResolveDestinationAccountAsync(
        AccountingPosting posting,
        AccountingPostingDestination destination,
        CancellationToken ct
    )
    {
        switch (destination.Kind)
        {
            case AccountingPostingDestination.CashRegister:
                var cash = await _cashRegisters.GetByIdAsync(posting.TenantId, destination.ReferenceId, ct);
                if (cash is null || cash.CompanyId != posting.CompanyId)
                    return Result<Guid>.ValidationFailure("La caja del hecho no existe en esta empresa.", DestinationNotFoundCode);
                return cash.AccountingAccountId is { } cashAccount
                    ? Result<Guid>.Success(cashAccount)
                    : Result<Guid>.ValidationFailure(
                        $"La caja '{cash.Name}' no tiene una cuenta contable configurada.",
                        DestinationAccountNotConfiguredCode
                    );

            case AccountingPostingDestination.BankAccount:
                var bank = await _bankAccounts.GetByIdAsync(posting.TenantId, destination.ReferenceId, ct);
                return bank is null || bank.CompanyId != posting.CompanyId
                    ? Result<Guid>.ValidationFailure("La cuenta bancaria del hecho no existe en esta empresa.", DestinationNotFoundCode)
                    : Result<Guid>.Success(bank.AccountingAccountId);

            case AccountingPostingDestination.PaymentMethod:
                var link = await _paymentMethodAccounts.GetAsync(posting.TenantId, posting.CompanyId, destination.ReferenceId, ct);
                return link is null
                    ? Result<Guid>.ValidationFailure(
                        "El método de pago no tiene una cuenta contable configurada para esta empresa.",
                        DestinationAccountNotConfiguredCode
                    )
                    : Result<Guid>.Success(link.AccountingAccountId);

            case AccountingPostingDestination.InventoryAdjustmentReason:
                // ACCOUNTING-12A — sin fallback: motivo sin cuenta o cuenta inválida deja Failed y se
                // corrige configurando el motivo y reprocesando.
                var reason = await _adjustmentReasons.GetByIdAsync(posting.TenantId, destination.ReferenceId, ct);
                if (reason is null || reason.CompanyId != posting.CompanyId)
                    return Result<Guid>.ValidationFailure("El motivo del ajuste no existe en esta empresa.", DestinationNotFoundCode);
                // Cuenta de la Company del hecho (configuración Company-scoped, nunca del maestro tenant-wide).
                var reasonLink = await _adjustmentReasonAccounts.GetAsync(posting.TenantId, posting.CompanyId, reason.Id, ct);
                if (reasonLink?.AccountingAccountId is not { } reasonAccountId)
                    return Result<Guid>.ValidationFailure(
                        $"El motivo de ajuste '{reason.Name}' no tiene una cuenta contable configurada para esta empresa.",
                        AdjustmentAccountNotConfiguredCode
                    );
                var reasonAccount = await _accounts.GetByIdAsync(posting.TenantId, posting.CompanyId, reasonAccountId, ct);
                if (reasonAccount is null || !reasonAccount.IsActive || !reasonAccount.AllowsPosting)
                    return Result<Guid>.ValidationFailure(
                        $"La cuenta contable del motivo de ajuste '{reason.Name}' no existe en esta empresa, está inactiva o no es imputable.",
                        AdjustmentAccountInvalidCode
                    );
                return Result<Guid>.Success(reasonAccount.Id);

            case AccountingPostingDestination.CashMovementReason:
                // ACCOUNTING-12C — el motivo es Tenant+Company: se busca en la Company del hecho y su
                // cuenta se resuelve con la configuración vigente, sin fallback.
                var cashReason = await _cashReasons.GetByIdAsync(posting.TenantId, posting.CompanyId, destination.ReferenceId, ct);
                if (cashReason is null)
                    return Result<Guid>.ValidationFailure("El motivo del movimiento de caja no existe en esta empresa.", DestinationNotFoundCode);
                if (cashReason.AccountingAccountId is not { } cashReasonAccountId)
                    return Result<Guid>.ValidationFailure(
                        $"El motivo de caja '{cashReason.Name}' no tiene una cuenta contable configurada.",
                        CashReasonAccountNotConfiguredCode
                    );
                var cashReasonAccount = await _accounts.GetByIdAsync(posting.TenantId, posting.CompanyId, cashReasonAccountId, ct);
                if (cashReasonAccount is null || !cashReasonAccount.IsActive || !cashReasonAccount.AllowsPosting)
                    return Result<Guid>.ValidationFailure(
                        $"La cuenta contable del motivo de caja '{cashReason.Name}' no existe en esta empresa, está inactiva o no es imputable.",
                        CashReasonAccountInvalidCode
                    );
                return Result<Guid>.Success(cashReasonAccount.Id);

            default:
                return Result<Guid>.ValidationFailure($"Destino financiero no soportado ({destination.Kind}).", PayloadUnsupportedCode);
        }
    }

    /// <summary>
    /// Un importe sin línea propia en la regla no puede contabilizarse: IRBPNR quedaría sin
    /// contrapartida y un anticipo se debitaría contra CxP. Sin regla, el motor informa su error.
    /// </summary>
    private async Task<Result<bool>> EnsureRequiredRuleLinesAsync(
        AccountingPosting posting,
        AccountingPostingPayload payload,
        CancellationToken ct
    )
    {
        var required = new List<(PostingAmountKind Kind, string Label)>();
        if (payload.TotalIrbpnr > 0m)
            required.Add((PostingAmountKind.TaxIrbpnr, "IRBPNR"));
        if (payload.SupplierCreditAmount > 0m)
            required.Add((PostingAmountKind.SupplierCredit, "anticipos/saldo a favor de proveedores"));
        if (required.Count == 0)
            return Result<bool>.Success(true);

        var rule = await _rules.FindByKeyAsync(posting.TenantId, posting.CompanyId, posting.SourceModule, posting.FactType, ct);
        if (rule is not { IsActive: true })
            return Result<bool>.Success(true);

        var missing = required.FirstOrDefault(r => rule.Lines.All(l => l.AmountKind != r.Kind));
        return missing == default
            ? Result<bool>.Success(true)
            : Result<bool>.ValidationFailure(
                $"La regla contable {posting.SourceModule}/{posting.FactType} no tiene configurada la línea de {missing.Label}.",
                RuleLineNotConfiguredCode
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

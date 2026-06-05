namespace ERP.Application.Modules.Cash.DTOs;

public sealed record BankAccountDto(
    Guid Id,
    string  Name,
    string AccountNumber,
    string AccountType,
    string Currency,
    decimal OpeningBalance,
    decimal CurrentBalance,
    bool IsActive,
    Guid? LedgerAccountId);

public sealed record BankStatementDto(
    Guid Id,
    Guid BankAccountId,
    DateTime PeriodFrom,
    DateTime PeriodTo,
    decimal OpeningBalance,
    decimal ClosingBalance,
    DateTime LoadedAt,
    bool IsReconciled,
    int RowCount);

public sealed record BankTransactionDto(
    Guid Id,
    Guid BankStatementId,
    DateTime TransactionDate,
    string  Description,
    decimal Amount,
    string TransactionType,
    string Reference,
    Guid?     JournalEntryId,
    string    Status);

public sealed record ReconciliationSuggestionDto(
    Guid BankTransactionId,
    Guid? SuggestedJournalEntryId,
    string? Reason);

public sealed record DailyCashFlowDto(DateOnly Date, decimal Inflows, decimal Outflows, decimal Net);

public sealed record PettyCashExpenseDto(
    Guid Id,
    Guid PettyCashId,
    DateTime ExpenseDate,
    string Description,
    decimal Amount,
    string VoucherType,
    string VoucherNumber,
    Guid?     JournalEntryId);

public sealed record PettyCashCountDto(
    Guid Id,
    Guid PettyCashId,
    DateTime CountDate,
    decimal PhysicalCash,
    decimal Variance,
    string Notes,
    bool IsApproved);

public sealed record PettyCashDto(
    Guid Id,
    string  Name,
    decimal AssignedBalance,
    decimal CurrentBalance,
    Guid? ReplenishmentBankAccountId,
    Guid? LedgerCashAccountId,
    bool IsActive);

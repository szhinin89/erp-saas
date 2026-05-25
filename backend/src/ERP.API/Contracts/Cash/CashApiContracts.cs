namespace ERP.API.Contracts.Cash;

public sealed class ImportStatementForm
{
    public IFormFile File { get; set; } = null!;
    public Guid BankAccountId { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
}

public sealed record CreateBankAccountRequest(
    string Name,
    string AccountNumber,
    string AccountType,
    string Currency,
    decimal InitialBalance,
    Guid? LedgerAccountId);

public sealed record ConciliarRequest(Guid JournalEntryId);

public sealed record CreatePettyCashRequest(
    string Name,
    decimal AssignedBalance,
    Guid? ReplenishmentBankAccountId,
    Guid? LedgerCashAccountId);

public sealed record CreatePettyCashExpenseRequest(
    Guid PettyCashId,
    DateTime Date,
    string Description,
    decimal Amount,
    string VoucherType,
    string? VoucherNumber);

public sealed record CreateCashCountRequest(
    Guid PettyCashId,
    DateTime CountDate,
    decimal PhysicalCash,
    string? Notes);

public sealed record PettyCashReplenishmentRequest(Guid PettyCashId, decimal Amount);

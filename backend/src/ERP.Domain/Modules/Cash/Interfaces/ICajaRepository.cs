using ERP.Domain.Modules.Cash.Entities;

namespace ERP.Domain.Modules.Cash.Interfaces;

public interface ICashRepository
{
    Task<BankAccount?> GetBankAccountByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<BankAccount>> ListBankAccountsAsync(CancellationToken ct = default);
    Task AddBankAccountAsync(BankAccount entity, CancellationToken ct = default);

    Task<BankStatement?> GetBankStatementByIdAsync(Guid id, CancellationToken ct = default);
    Task<BankStatement?> GetBankStatementWithTransactionsAsync(Guid id, CancellationToken ct = default);
    Task<BankStatement?> GetBankStatementForTransactionAsync(Guid transactionId, CancellationToken ct = default);
    Task<IReadOnlyList<BankStatement>> ListStatementsByAccountAsync(Guid bankAccountId, CancellationToken ct = default);
    Task AddBankStatementAsync(BankStatement entity, CancellationToken ct = default);

    Task<BankTransaction?> GetBankTransactionByIdAsync(Guid id, CancellationToken ct = default);

    Task<PettyCash?> GetPettyCashByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PettyCash>> ListPettyCashesAsync(CancellationToken ct = default);
    Task AddPettyCashAsync(PettyCash entity, CancellationToken ct = default);

    Task AddCashCountAsync(CashCount entity, CancellationToken ct = default);
    Task<CashCount?> GetCashCountByIdAsync(Guid id, CancellationToken ct = default);
    Task AddPettyCashExpenseAsync(PettyCashExpense entity, CancellationToken ct = default);
    Task<IReadOnlyList<PettyCashExpense>> ListPettyCashExpensesAsync(Guid pettyCashId, CancellationToken ct = default);
}

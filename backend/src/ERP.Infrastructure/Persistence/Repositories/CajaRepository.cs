using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Cash.Entities;
using ERP.Domain.Modules.Cash.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CajaRepository : ICashRepository
{
    private readonly ErpDbContext _db;

    public CajaRepository(ErpDbContext db) => _db = db;

    public Task<BankAccount?> GetBankAccountByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BankAccounts.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<BankAccount>> ListBankAccountsAsync(CancellationToken ct = default)
        => await _db.BankAccounts.OrderBy(x => x.Name).ToListAsync(ct);

    public Task AddBankAccountAsync(BankAccount entity, CancellationToken ct = default)
        => _db.BankAccounts.AddAsync(entity, ct).AsTask();

    public Task<BankStatement?> GetBankStatementByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BankStatements.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<BankStatement?> GetBankStatementWithTransactionsAsync(Guid id, CancellationToken ct = default)
        => _db.BankStatements
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<BankStatement?> GetBankStatementForTransactionAsync(
        Guid movimientoId,
        CancellationToken ct = default)
    {
        var mov = await _db.BankTransactions.AsNoTracking().FirstOrDefaultAsync(m => m.Id == movimientoId, ct);
        if (mov is null)
            return null;
        return await GetBankStatementWithTransactionsAsync(mov.BankStatementId, ct);
    }

    public async Task<IReadOnlyList<BankStatement>> ListStatementsByAccountAsync(
        Guid BankAccountId,
        CancellationToken ct = default)
        => await _db.BankStatements
            .Include(x => x.Transactions)
            .Where(x => x.BankAccountId == BankAccountId)
            .OrderByDescending(x => x.LoadedAt)
            .ToListAsync(ct);

    public Task AddBankStatementAsync(BankStatement entity, CancellationToken ct = default)
        => _db.BankStatements.AddAsync(entity, ct).AsTask();

    public Task<BankTransaction?> GetBankTransactionByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BankTransactions.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<PettyCash?> GetPettyCashByIdAsync(Guid id, CancellationToken ct = default)
        => _db.PettyCashes.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PettyCash>> ListPettyCashesAsync(CancellationToken ct = default)
        => await _db.PettyCashes.OrderBy(x => x.Name).ToListAsync(ct);

    public Task AddPettyCashAsync(PettyCash entity, CancellationToken ct = default)
        => _db.PettyCashes.AddAsync(entity, ct).AsTask();

    public Task AddCashCountAsync(CashCount entity, CancellationToken ct = default)
        => _db.CashCounts.AddAsync(entity, ct).AsTask();

    public Task<CashCount?> GetCashCountByIdAsync(Guid id, CancellationToken ct = default)
        => _db.CashCounts.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task AddPettyCashExpenseAsync(PettyCashExpense entity, CancellationToken ct = default)
        => _db.PettyCashExpenses.AddAsync(entity, ct).AsTask();

    public async Task<IReadOnlyList<PettyCashExpense>> ListPettyCashExpensesAsync(Guid PettyCashId, CancellationToken ct = default)
        => await _db.PettyCashExpenses
            .Where(x => x.PettyCashId == PettyCashId)
            .OrderByDescending(x => x.ExpenseDate)
            .ToListAsync(ct);
}

using ERP.Domain.Modules.Accounting.Entities;

namespace ERP.Domain.Modules.Accounting.Interfaces;

public interface IAccountingSetupRepository
{
    Task<AccountingSetup?> GetSetupAsync(CancellationToken ct = default);
    Task AddSetupAsync(AccountingSetup entity, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseCategory>> GetExpenseCategoriesAsync(CancellationToken ct = default);
    Task<ExpenseCategory?> GetExpenseCategoryByCategoryAsync(string category, CancellationToken ct = default);
    Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid id, CancellationToken ct = default);
    Task AddExpenseCategoryAsync(ExpenseCategory entity, CancellationToken ct = default);
    void RemoveExpenseCategory(ExpenseCategory entity);
    Task SaveChangesAsync(CancellationToken ct = default);
}

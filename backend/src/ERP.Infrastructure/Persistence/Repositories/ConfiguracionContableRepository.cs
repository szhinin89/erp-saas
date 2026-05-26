using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class AccountingConfigurationRepository : IAccountingSetupRepository
{
    private readonly ErpDbContext _context;

    public AccountingConfigurationRepository(ErpDbContext context)
    {
        _context = context;
    }

    public async Task<AccountingSetup?> GetSetupAsync(CancellationToken ct = default)
        => await _context.AccountingSetups.FirstOrDefaultAsync(ct);

    public async Task AddSetupAsync(AccountingSetup entity, CancellationToken ct = default)
        => await _context.AccountingSetups.AddAsync(entity, ct);

    public async Task<IReadOnlyList<ExpenseCategory>> GetExpenseCategoriesAsync(CancellationToken ct = default)
        => await _context.ExpenseCategories
            .OrderBy(g => g.Category)
            .ToListAsync(ct);

    public async Task<ExpenseCategory?> GetExpenseCategoryByCategoryAsync(string categoria, CancellationToken ct = default)
    {
        var c = categoria.Trim();
        return await _context.ExpenseCategories
            .FirstOrDefaultAsync(
                g => g.Category.ToLower() == c.ToLower(),
                ct);
    }

    public async Task<ExpenseCategory?> GetExpenseCategoryByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.ExpenseCategories.FirstOrDefaultAsync(g => g.Id == id, ct);

    public async Task AddExpenseCategoryAsync(ExpenseCategory entity, CancellationToken ct = default)
        => await _context.ExpenseCategories.AddAsync(entity, ct);

    public void RemoveExpenseCategory(ExpenseCategory entity)
        => _context.ExpenseCategories.Remove(entity);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

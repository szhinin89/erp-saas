using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;

namespace ERP.Domain.Modules.Expenses.Interfaces;

public interface IExpenseInvoiceRepository
{
    Task AddAsync(ExpenseInvoice expense, CancellationToken ct = default);
    Task<ExpenseInvoice?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<bool> ExistsAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default);
    Task<IReadOnlyList<ExpenseInvoice>> GetAsync(
        Guid          subscriberId,
        ExpenseStatus? status,
        Guid?          supplierId,
        DateTime?      from,
        DateTime?      to,
        string?        search,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

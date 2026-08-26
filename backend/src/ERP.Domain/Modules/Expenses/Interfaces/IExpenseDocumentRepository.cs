using ERP.Domain.Modules.Expenses.Entities;

namespace ERP.Domain.Modules.Expenses.Interfaces;

public interface IExpenseDocumentRepository
{
    Task<ExpenseDocument?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<ExpenseDocument?> GetBySupplierAndDocumentNumberAsync(
        Guid tenantId,
        Guid supplierId,
        string documentType,
        string documentNumber,
        CancellationToken ct = default
    );

    Task AddAsync(ExpenseDocument document, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

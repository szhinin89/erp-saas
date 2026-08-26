using ERP.Application.Common;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Expenses;

public sealed class ExpenseDocumentRepository : IExpenseDocumentRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public ExpenseDocumentRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<ExpenseDocument> Scoped(Guid tenantId) =>
        _db.ExpenseDocuments.ForOperationalScope(tenantId, _company);

    public Task<ExpenseDocument?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .Include(x => x.Lines.OrderBy(l => l.SortOrder))
            .Include(x => x.PaymentSchedules.OrderBy(s => s.InstallmentNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ExpenseDocument?> GetBySupplierAndDocumentNumberAsync(
        Guid tenantId,
        Guid supplierId,
        string documentType,
        string documentNumber,
        CancellationToken ct = default
    ) =>
        Scoped(tenantId)
            .FirstOrDefaultAsync(
                x =>
                    x.SupplierId == supplierId
                    && x.DocumentType == documentType
                    && x.DocumentNumber == documentNumber,
                ct
            );

    public Task AddAsync(ExpenseDocument document, CancellationToken ct = default) =>
        _db.ExpenseDocuments.AddAsync(document, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

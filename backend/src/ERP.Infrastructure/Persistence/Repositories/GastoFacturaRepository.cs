using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ExpenseInvoiceRepository : IExpenseInvoiceRepository
{
    private readonly ErpDbContext _context;

    public ExpenseInvoiceRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(ExpenseInvoice gasto, CancellationToken ct = default)
        => _context.ExpenseInvoices.AddAsync(gasto, ct).AsTask();

    public Task<ExpenseInvoice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.ExpenseInvoices.FirstOrDefaultAsync(g => g.TenantId == tenantId && g.Id == id, ct);

    public Task<bool> ExistsAccessKeyAsync(Guid tenantId, string accessKey, CancellationToken ct = default)
        => _context.ExpenseInvoices.AnyAsync(
            g => g.TenantId == tenantId && g.AccessKey == accessKey, ct);

    public async Task<IReadOnlyList<ExpenseInvoice>> GetAsync(
        Guid tenantId,
        ExpenseStatus? estado,
        Guid? proveedorId,
        DateTime? desde,
        DateTime? hasta,
        string? search,
        CancellationToken ct = default)
    {
        var q = _context.ExpenseInvoices.Where(g => g.TenantId == tenantId);

        if (estado.HasValue)       q = q.Where(g => g.Status == estado.Value);
        if (proveedorId.HasValue) q = q.Where(g => g.SupplierId == proveedorId.Value);
        if (desde.HasValue)        q = q.Where(g => g.IssueDate >= desde.Value.Date);
        if (hasta.HasValue)        q = q.Where(g => g.IssueDate <= hasta.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(g =>
                g.Concept.ToLower().Contains(s) ||
                (g.AccessKey != null && g.AccessKey.Contains(s)) ||
                (g.InvoiceNumber != null && g.InvoiceNumber.ToLower().Contains(s)));
        }

        return await q.OrderByDescending(g => g.IssueDate).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

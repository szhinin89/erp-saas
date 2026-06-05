using Microsoft.EntityFrameworkCore;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Modules.Expenses.Entities;
using ERP.Domain.Modules.Expenses.Enums;
using ERP.Domain.Modules.Expenses.Interfaces;
using ERP.Infrastructure.Persistence.Mapping;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class ExpenseInvoiceRepository : IExpenseInvoiceRepository
{
    private readonly ErpDbContext _context;
    private readonly IUnifiedDocumentSync _documentSync;

    public ExpenseInvoiceRepository(
        ErpDbContext context,
        IUnifiedDocumentSync documentSync)
    {
        _context = context;
        _documentSync = documentSync;
    }

    public Task AddAsync(ExpenseInvoice gasto, CancellationToken ct = default)
        => _context.ExpenseDocuments.AddAsync(ExpenseDocumentMapper.ToDocument(gasto), ct).AsTask();

    public async Task<ExpenseInvoice?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
    {
        var doc = await _context.ExpenseDocuments
            .Include(g => g.Details)
            .FirstOrDefaultAsync(g => g.SubscriberId == subscriberId && g.Id == id, ct);
        if (doc is null) return null;
        var invoice = ExpenseDocumentMapper.ToLegacyInvoice(doc);
        _documentSync.StageExpenseInvoice(invoice);
        return invoice;
    }

    public Task<bool> ExistsAccessKeyAsync(Guid subscriberId, string accessKey, CancellationToken ct = default)
        => _context.ExpenseDocuments.AnyAsync(
            g => g.SubscriberId == subscriberId && g.AccessKey == accessKey, ct);

    public async Task<IReadOnlyList<ExpenseInvoice>> GetAsync(
        Guid subscriberId,
        ExpenseStatus? status,
        Guid? proveedorId,
        DateTime? desde,
        DateTime? hasta,
        string? search,
        CancellationToken ct = default)
    {
        var q = _context.ExpenseDocuments.Where(g => g.SubscriberId == subscriberId);

        if (status.HasValue)
            q = q.Where(g => g.Status == status.Value);
        if (proveedorId.HasValue)
            q = q.Where(g => g.BusinessPartnerId == proveedorId.Value);
        if (desde.HasValue)
            q = q.Where(g => g.IssueDate >= desde.Value.Date);
        if (hasta.HasValue)
            q = q.Where(g => g.IssueDate <= hasta.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(g =>
                g.Concept.ToLower().Contains(s) ||
                (g.AccessKey != null && g.AccessKey.Contains(s)) ||
                (g.DocNumber != null && g.DocNumber.ToLower().Contains(s)));
        }

        var docs = await q.OrderByDescending(g => g.IssueDate).ToListAsync(ct);
        return docs.Select(ExpenseDocumentMapper.ToLegacyInvoice).ToList();
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _documentSync.FlushAsync(ct);
        await _context.SaveChangesAsync(ct);
    }
}

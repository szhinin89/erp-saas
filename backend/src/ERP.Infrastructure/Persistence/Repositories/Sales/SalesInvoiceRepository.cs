using Microsoft.EntityFrameworkCore;
using ERP.Application.Common;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Enums;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories.Sales;

public sealed class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly ErpDbContext _db;
    private readonly ICurrentCompany _company;

    public SalesInvoiceRepository(ErpDbContext db, ICurrentCompany company)
    {
        _db = db;
        _company = company;
    }

    private IQueryable<SalesInvoice> Scoped(Guid tenantId)
        => _db.SalesInvoices.ForOperationalScope(tenantId, _company);

    public Task<SalesInvoice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Scoped(tenantId)
            .Include(x => x.Lines.OrderBy(l => l.SortOrder))
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<SalesInvoice> Items, int Total)> GetPagedAsync(
        Guid tenantId, string? search, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = Scoped(tenantId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SalesInvoiceStatus>(status.Trim(), true, out var ss))
            q = q.Where(x => x.Status == ss);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => x.InvoiceNumber.Contains(s) || x.Customer.Name.Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.Lines)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyDictionary<Guid, (string InvoiceNumber, string CustomerName)>> GetSummariesByIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, (string, string)>();

        var rows = await Scoped(tenantId)
            .AsNoTracking()
            .Where(x => ids.Contains(x.Id))
            .Select(x => new { x.Id, x.InvoiceNumber, CustomerName = x.Customer.Name })
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.Id, x => (x.InvoiceNumber, x.CustomerName));
    }

    public Task AddAsync(SalesInvoice invoice, CancellationToken ct = default)
        => _db.SalesInvoices.AddAsync(invoice, ct).AsTask();

    public async Task RemoveLinesByInvoiceAsync(Guid invoiceId, IEnumerable<SalesInvoiceDetail> newLines, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM sales_invoice_details WHERE invoice_id = {invoiceId}", ct);

        foreach (var entry in _db.ChangeTracker.Entries<SalesInvoiceDetail>().ToList())
            entry.State = EntityState.Detached;

        foreach (var line in newLines)
            _db.Set<SalesInvoiceDetail>().Add(line);
    }

    public async Task RemovePaymentsByInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM sales_invoice_payments WHERE sales_invoice_id = {invoiceId}", ct);

        foreach (var entry in _db.ChangeTracker.Entries<SalesInvoicePayment>().ToList())
            entry.State = EntityState.Detached;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

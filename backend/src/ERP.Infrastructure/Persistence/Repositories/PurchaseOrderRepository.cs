using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly ErpDbContext _context;

    public PurchaseOrderRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(PurchaseOrder orden, CancellationToken ct = default)
        => _context.PurchaseOrders.AddAsync(orden, ct).AsTask();

    public Task<PurchaseOrder?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.PurchaseOrders
            .FirstOrDefaultAsync(o => o.SubscriberId == subscriberId && o.Id == id, ct);

    public Task<PurchaseOrder?> GetByIdWithLinesAsync(Guid subscriberId, Guid id, CancellationToken ct = default)
        => _context.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.SubscriberId == subscriberId && o.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var max = await _context.PurchaseOrders
            .Where(o => o.SubscriberId == subscriberId)
            .MaxAsync(o => (int?)o.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<PurchaseOrder> Items, int TotalCount)> GetPagedAsync(
        Guid      subscriberId,
        int       pageNumber,
        int       pageSize,
        Guid?     proveedorId,
        string? status,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = _context.PurchaseOrders
            .Where(o => o.SubscriberId == subscriberId);

        if (proveedorId.HasValue)
            query = query.Where(o => o.BusinessPartnerId == proveedorId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(o => o.Status == status);
        if (fechaDesde.HasValue)
            query = query.Where(o => o.IssueDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(o => o.IssueDate <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.IssueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<IReadOnlyList<PurchaseOrder>> GetPendingToInvoiceAsync(
        Guid subscriberId, CancellationToken ct = default)
        => _context.PurchaseOrders
            .Where(o => o.SubscriberId == subscriberId
                && (o.Status == "Aprobada" || o.Status == "RecibidaParcial")
                && o.Lines.Any(d => d.InvoicedQty < d.OrderedQty))
            .OrderBy(o => o.IssueDate)
            .ToListAsync(ct)
            .ContinueWith(t => (IReadOnlyList<PurchaseOrder>)t.Result, TaskContinuationOptions.ExecuteSynchronously);

    public Task<bool> BillAlreadyLinkedAsync(
        Guid subscriberId, Guid OrderId, Guid facturaId, CancellationToken ct = default)
        => _context.PurchaseOrderBills
            .AnyAsync(v => v.SubscriberId == subscriberId
                && v.PurchaseOrderId == OrderId
                && v.PurchBillId == facturaId, ct);

    public async Task<IReadOnlyList<(Guid PurchBillId, string InvoiceNumber, DateTime LinkedAt)>>
        GetBillLinksAsync(Guid subscriberId, Guid OrderId, CancellationToken ct = default)
    {
        var results = await _context.PurchaseOrderBills
            .Where(v => v.SubscriberId == subscriberId && v.PurchaseOrderId == OrderId)
            .Join(_context.PurchBills,
                  v => v.PurchBillId,
                  f => f.Id,
                  (v, f) => new { v.PurchBillId, f.InvoiceNumber, v.LinkedAt })
            .ToListAsync(ct);

        return results
            .Select(x => (x.PurchBillId, x.InvoiceNumber, x.LinkedAt))
            .ToList();
    }

    public Task AddOrderBillLinkAsync(PurchaseOrderBill vinculo, CancellationToken ct = default)
        => _context.PurchaseOrderBills.AddAsync(vinculo, ct).AsTask();

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

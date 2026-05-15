using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Purchasing.Entities;
using ERP.Domain.Modules.Purchasing.Enums;
using ERP.Domain.Modules.Purchasing.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class CompraRepository : IPurchBillRepository
{
    private readonly ErpDbContext _context;

    public CompraRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(PurchBill compra, CancellationToken ct = default)
        => _context.PurchBills.AddAsync(compra, ct).AsTask();

    public Task<PurchBill?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.PurchBills
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<PurchBill?> GetByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.PurchBills
            .Include(c => c.Lines)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public Task<bool> ExistsAccessKeyAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default)
        => _context.PurchBills
            .AnyAsync(c => c.TenantId == tenantId && c.AccessKey == claveAcceso, ct);

    public async Task<IReadOnlyList<PurchBill>> GetAsync(
        Guid tenantId,
        PurchaseStatus? estado,
        Guid?         proveedorId,
        DateTime?     desde,
        DateTime?     hasta,
        string?       search,
        CancellationToken ct = default)
    {
        var q = _context.PurchBills.Where(c => c.TenantId == tenantId);

        if (estado.HasValue)        q = q.Where(c => c.Status == estado.Value);
        if (proveedorId.HasValue)   q = q.Where(c => c.SupplierId == proveedorId.Value);
        if (desde.HasValue)         q = q.Where(c => c.InvoiceDate >= desde.Value.Date);
        if (hasta.HasValue)         q = q.Where(c => c.InvoiceDate <= hasta.Value.Date);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(c =>
                c.InvoiceNumber.ToLower().Contains(s) ||
                (c.AccessKey != null && c.AccessKey.Contains(s)));
        }

        return await q.OrderByDescending(c => c.InvoiceDate).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PurchWarehouseAlloc>> GetWarehouseAllocsByBillIdAsync(
        Guid tenantId,
        Guid PurchBillId,
        CancellationToken ct = default)
        => await _context.PurchWarehouseAllocs
            .Where(a => a.TenantId == tenantId && a.PurchBillId == PurchBillId)
            .OrderBy(a => a.PurchBillLineId)
            .ThenBy(a => a.WarehouseId)
            .ToListAsync(ct);

    public Task AddWarehouseAllocAsync(PurchWarehouseAlloc asignacion, CancellationToken ct = default)
        => _context.PurchWarehouseAllocs.AddAsync(asignacion, ct).AsTask();

    public Task AddIssuedRetentionAsync(IssuedRetention retencion, CancellationToken ct = default)
        => _context.IssuedRetentions.AddAsync(retencion, ct).AsTask();

    public Task<IssuedRetention?> GetIssuedRetentionByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.IssuedRetentions
            .Include(r => r.Supplier)
            .Include(r => r.Lines)
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public async Task<IReadOnlyList<IssuedRetention>> GetIssuedRetentionsAsync(
        Guid tenantId,
        Guid? proveedorId,
        CancellationToken ct = default)
    {
        var q = _context.IssuedRetentions
            .Include(r => r.Supplier)
            .Where(r => r.TenantId == tenantId);
        if (proveedorId.HasValue)
            q = q.Where(r => r.SupplierId == proveedorId.Value);
        return await q.OrderByDescending(r => r.IssueDate).ToListAsync(ct);
    }

    public Task AddPurchNoteAsync(PurchNote nota, CancellationToken ct = default)
        => _context.PurchNotes.AddAsync(nota, ct).AsTask();

    public Task<PurchNote?> GetPurchNoteByIdWithLinesAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
        => _context.PurchNotes
            .Include(n => n.Lines)
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);

    public Task<bool> ExistsPurchNoteAccessKeyAsync(
        Guid tenantId,
        string claveAcceso,
        CancellationToken ct = default)
        => _context.PurchNotes.AnyAsync(
            n => n.TenantId == tenantId && n.AccessKey == claveAcceso, ct);

    public async Task<IReadOnlyList<PurchNote>> GetPurchNotesAsync(
        Guid tenantId,
        Guid? proveedorId,
        Guid? PurchBillId,
        Guid? ExpenseInvoiceId,
        string? estado,
        CancellationToken ct = default)
    {
        var q = _context.PurchNotes.Where(n => n.TenantId == tenantId);
        if (proveedorId.HasValue) q = q.Where(n => n.SupplierId == proveedorId.Value);
        if (PurchBillId.HasValue) q = q.Where(n => n.PurchBillId == PurchBillId.Value);
        if (ExpenseInvoiceId.HasValue) q = q.Where(n => n.ExpenseInvoiceId == ExpenseInvoiceId.Value);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            var e = estado.Trim();
            q = q.Where(n => n.Status == e);
        }

        return await q.OrderByDescending(n => n.IssueDate).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Sales.Entities;
using ERP.Domain.Modules.Sales.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class SalesRepository : ISalesRepository
{
    private readonly ErpDbContext _context;

    public SalesRepository(ErpDbContext context) => _context = context;

    public Task AddBillAsync(SalesBill factura, CancellationToken ct = default)
        => _context.SalesBills.AddAsync(factura, ct).AsTask();

    public Task<SalesBill?> GetBillByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.SalesBills
            .Include(f => f.Cliente)
            .Include(f => f.Lines)
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == id, ct);

    public async Task<IReadOnlyList<SalesBill>> GetBillsAsync(
        Guid tenantId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        CancellationToken ct = default)
    {
        var query = _context.SalesBills
            .Include(f => f.Cliente)
            .Where(f => f.TenantId == tenantId);

        if (fechaDesde.HasValue)
            query = query.Where(f => f.IssueDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(f => f.IssueDate <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(f => f.Status == estado);

        return await query.ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<SalesBill> Items, int TotalCount)> GetBillsPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? clienteId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        string? estado,
        string? search,
        CancellationToken ct = default)
    {
        var query = _context.SalesBills
            .Include(f => f.Cliente)
            .Where(f => f.TenantId == tenantId);

        if (clienteId.HasValue)
            query = query.Where(f => f.CustomerId == clienteId.Value);
        if (fechaDesde.HasValue)
            query = query.Where(f => f.IssueDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(f => f.IssueDate <= fechaHasta.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(f => f.Status == estado);
        if (!string.IsNullOrEmpty(search))
            query = query.Where(f =>
                f.Sequential.Contains(search) ||
                f.AccessKey.Contains(search) ||
                f.AuthNumber != null && f.AuthNumber.Contains(search));

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(f => f.IssueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task AddNoteAsync(SalesNote nota, CancellationToken ct = default)
        => _context.SalesNotes.AddAsync(nota, ct).AsTask();

    public Task<SalesNote?> GetNoteByIdWithLinesAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.SalesNotes
            .Include(n => n.OriginalBill)
                .ThenInclude(f => f.Cliente)
            .Include(n => n.Lines)
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id, ct);

    public async Task<IReadOnlyList<SalesNote>> GetNotesAsync(
        Guid tenantId,
        Guid? facturaOriginalId,
        string? estado,
        CancellationToken ct = default)
    {
        var q = _context.SalesNotes
            .Include(n => n.OriginalBill)
                .ThenInclude(f => f.Cliente)
            .Where(n => n.TenantId == tenantId);

        if (facturaOriginalId.HasValue)
            q = q.Where(n => n.OriginalBillId == facturaOriginalId.Value);
        if (!string.IsNullOrWhiteSpace(estado))
            q = q.Where(n => n.Status == estado);

        return await q.OrderByDescending(n => n.IssueDate).ToListAsync(ct);
    }

    public Task AddRetentionAsync(SalesRetention retencion, CancellationToken ct = default)
        => _context.SalesRetentions.AddAsync(retencion, ct).AsTask();

    public async Task<IReadOnlyList<SalesRetention>> GetRetentionsAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => await _context.SalesRetentions
            .Include(r => r.Customer)
            .Include(r => r.Lines)
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.IssueDate)
            .ToListAsync(ct);

    public Task<bool> ExistsRetentionAccessKeyAsync(Guid tenantId, string claveAcceso, CancellationToken ct = default)
        => _context.SalesRetentions.AnyAsync(
            r => r.TenantId == tenantId && r.AccessKey == claveAcceso, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

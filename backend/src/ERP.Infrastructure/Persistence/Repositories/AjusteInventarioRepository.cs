using Microsoft.EntityFrameworkCore;
using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly ErpDbContext _context;

    public StockAdjustmentRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(StockAdjustment ajuste, CancellationToken ct = default)
        => _context.StockAdjustments.AddAsync(ajuste, ct).AsTask();

    public Task<StockAdjustment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _context.StockAdjustments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default)
    {
        // MaxAsync nullable — compatible con PostgreSQL e InMemory
        var max = await _context.StockAdjustments
            .Where(a => a.TenantId == tenantId)
            .MaxAsync(a => (int?)a.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     WarehouseId,
        Guid?     productoId,
        string?   estado,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        CancellationToken ct = default)
    {
        var query = _context.StockAdjustments
            .Where(a => a.TenantId == tenantId);

        if (WarehouseId.HasValue)
            query = query.Where(a => a.WarehouseId == WarehouseId.Value);
        if (productoId.HasValue)
            query = query.Where(a => a.ProductId == productoId.Value);
        if (!string.IsNullOrEmpty(estado))
            query = query.Where(a => a.Status == estado);
        if (fechaDesde.HasValue)
            query = query.Where(a => a.AdjustmentDate >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            query = query.Where(a => a.AdjustmentDate <= fechaHasta.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

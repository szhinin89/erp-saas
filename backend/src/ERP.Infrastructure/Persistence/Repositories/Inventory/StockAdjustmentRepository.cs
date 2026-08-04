using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class StockAdjustmentRepository : IStockAdjustmentRepository
{
    private readonly ErpDbContext _db;

    public StockAdjustmentRepository(ErpDbContext db) => _db = db;

    public async Task AddAsync(StockAdjustment adjustment, CancellationToken ct = default) =>
        await _db.Set<StockAdjustment>().AddAsync(adjustment, ct);

    public Task<StockAdjustment?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default
    ) =>
        _db.Set<StockAdjustment>()
            .Include(a => a.Lines)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default)
    {
        var max = await _db.Set<StockAdjustment>()
            .IgnoreQueryFilters()
            .Where(a => a.TenantId == tenantId)
            .MaxAsync(a => (int?)a.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? warehouseId,
        Guid? productId,
        string? status,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken ct = default
    )
    {
        var q = _db.Set<StockAdjustment>().Where(a => a.TenantId == tenantId);
        if (warehouseId.HasValue)
            q = q.Where(a => a.WarehouseId == warehouseId.Value);
        if (productId.HasValue)
            q = q.Where(a => a.ProductId == productId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);
        if (startDate.HasValue)
            q = q.Where(a => a.AdjustmentDate >= startDate.Value);
        if (endDate.HasValue)
            q = q.Where(a => a.AdjustmentDate <= endDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(a => a.Lines)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

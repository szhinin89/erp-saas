using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Persistence.Repositories.Inventory;

public sealed class StockTransferRepository : IStockTransferRepository
{
    private readonly ErpDbContext _db;

    public StockTransferRepository(ErpDbContext db) => _db = db;

    public async Task AddAsync(StockTransfer transfer, CancellationToken ct = default)
        => await _db.Set<StockTransfer>().AddAsync(transfer, ct);

    public Task<StockTransfer?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken ct = default)
        => _db.Set<StockTransfer>()
            .Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.CompanyId == companyId && t.Id == id, ct);

    public async Task<int> GetNextSequentialAsync(Guid tenantId, Guid companyId, CancellationToken ct = default)
    {
        var max = await _db.Set<StockTransfer>()
            .Where(t => t.TenantId == tenantId && t.CompanyId == companyId)
            .MaxAsync(t => (int?)t.Sequential, ct);
        return (max ?? 0) + 1;
    }

    public async Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId, Guid companyId, int pageNumber, int pageSize,
        Guid? sourceWarehouseId, Guid? targetWarehouseId, string? status,
        DateTime? startDate, DateTime? endDate, CancellationToken ct = default)
    {
        var q = _db.Set<StockTransfer>().Where(t => t.TenantId == tenantId && t.CompanyId == companyId);
        if (sourceWarehouseId.HasValue) q = q.Where(t => t.SourceWarehouseId == sourceWarehouseId.Value);
        if (targetWarehouseId.HasValue) q = q.Where(t => t.TargetWarehouseId == targetWarehouseId.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(t => t.Status == status);
        if (startDate.HasValue) q = q.Where(t => t.TransferDate >= startDate.Value);
        if (endDate.HasValue) q = q.Where(t => t.TransferDate <= endDate.Value);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Include(t => t.Lines)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

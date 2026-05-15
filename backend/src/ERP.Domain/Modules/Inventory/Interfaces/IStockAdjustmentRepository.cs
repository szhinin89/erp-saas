using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockAdjustmentRepository
{
    Task AddAsync(StockAdjustment adjustment, CancellationToken ct = default);
    Task<StockAdjustment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
        int       pageNumber,
        int       pageSize,
        Guid?     warehouseId,
        Guid?     productId,
        string?   status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

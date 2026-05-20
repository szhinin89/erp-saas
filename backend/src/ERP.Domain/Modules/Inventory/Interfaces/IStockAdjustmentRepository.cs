using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockAdjustmentRepository
{
    Task AddAsync(StockAdjustment adjustment, CancellationToken ct = default);
    Task<StockAdjustment?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<int> GetNextSequentialAsync(Guid subscriberId, CancellationToken ct = default);
    Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid      subscriberId,
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

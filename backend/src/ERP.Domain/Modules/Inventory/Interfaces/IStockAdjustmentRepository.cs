using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockAdjustmentRepository
{
    Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default);
    Task<StockAdjustment?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? warehouseId,
        Guid? productId,
        string? status,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default
    );
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

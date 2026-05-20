using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockTransferRepository
{
    Task AddAsync(StockTransfer transfer, CancellationToken ct = default);
    Task<StockTransfer?> GetByIdAsync(Guid subscriberId, Guid id, CancellationToken ct = default);
    Task<int> GetNextSequentialAsync(Guid subscriberId, CancellationToken ct = default);
    Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        Guid      subscriberId,
        int       pageNumber,
        int       pageSize,
        Guid?     sourceWarehouseId,
        Guid?     targetWarehouseId,
        string?   status,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

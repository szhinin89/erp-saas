using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockTransferRepository
{
    Task AddAsync(StockTransfer transfer, CancellationToken ct = default);
    Task<StockTransfer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken ct = default);
    Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        Guid      tenantId,
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

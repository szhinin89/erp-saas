using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockTransferRepository
{
    Task AddAsync(StockTransfer transfer, CancellationToken cancellationToken = default);
    Task<StockTransfer?> GetByIdAsync(Guid tenantId, Guid companyId, Guid id, CancellationToken cancellationToken = default);
    Task<int> GetNextSequentialAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StockTransfer> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        Guid companyId,
        int pageNumber,
        int pageSize,
        Guid? sourceWarehouseId,
        Guid? targetWarehouseId,
        string? status,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockRepository
{
    Task<CurrentStock?> GetStockAsync(
        Guid subscriberId, Guid warehouseId, Guid productId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CurrentStock>> GetStockByWarehouseAsync(
        Guid  subscriberId, Guid warehouseId, Guid? productId,
        CancellationToken ct = default);

    Task AddCurrentStockAsync(CurrentStock entity, CancellationToken ct = default);

    Task AddMovementAsync(StockMovement movement, CancellationToken ct = default);

    Task<decimal?> DecrementStockAtomicAsync(
        Guid subscriberId, Guid warehouseId, Guid productId,
        decimal delta, Guid updatedBy,
        CancellationToken ct = default,
        decimal unitCost = 0m);

    Task<decimal> IncrementStockAtomicAsync(
        Guid subscriberId, Guid warehouseId, Guid productId,
        decimal delta, Guid createdBy,
        CancellationToken ct = default,
        decimal unitCost = 0m);

    Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
        Guid      subscriberId,
        Guid      productId,
        Guid      warehouseId,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default);
}

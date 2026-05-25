using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IKardexSnapshotRepository
{
    Task<KardexSnapshot?> GetLatestBeforeAsync(
        Guid     subscriberId,
        Guid     productId,
        Guid     warehouseId,
        DateTime toUtc,
        CancellationToken ct = default);

    Task UpsertAsync(KardexSnapshot snapshot, CancellationToken ct = default);

    Task<IReadOnlyList<(Guid ProductId, Guid WarehouseId)>> GetDistinctProductWarehouseAsync(
        Guid subscriberId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetSubscribersWithMovementsAsync(CancellationToken ct = default);
}

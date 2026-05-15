using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IKardexSnapshotRepository
{
    Task<KardexSnapshot?> GetLatestBeforeAsync(
        Guid     tenantId,
        Guid     productId,
        Guid     warehouseId,
        DateTime toUtc,
        CancellationToken ct = default);

    Task UpsertAsync(KardexSnapshot snapshot, CancellationToken ct = default);

    Task<IReadOnlyList<(Guid ProductId, Guid WarehouseId)>> GetDistinctProductWarehouseAsync(
        Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetTenantsWithMovementsAsync(CancellationToken ct = default);
}

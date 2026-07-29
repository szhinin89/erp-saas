using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IKardexSnapshotRepository
{
    Task<KardexSnapshot?> GetLatestBeforeAsync(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        DateTime toUtc,
        CancellationToken cancellationToken = default
    );

    Task UpsertAsync(KardexSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Guid ProductId, Guid WarehouseId)>> GetDistinctProductWarehouseAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Guid>> GetTenantsWithMovementsAsync(
        CancellationToken cancellationToken = default
    );
}

using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

/// <summary>ACCOUNTING-12A — persistencia de <see cref="InventoryAdjustmentReasonAccount"/> (Company-scoped).</summary>
public interface IInventoryAdjustmentReasonAccountRepository
{
    Task<InventoryAdjustmentReasonAccount?> GetAsync(
        Guid tenantId, Guid companyId, Guid inventoryAdjustmentReasonId, CancellationToken ct = default);

    /// <summary>Mapa (motivo → configuración) de la Company indicada.</summary>
    Task<IReadOnlyDictionary<Guid, InventoryAdjustmentReasonAccount>> GetMapAsync(
        Guid tenantId, Guid companyId, CancellationToken ct = default);

    Task AddAsync(InventoryAdjustmentReasonAccount entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

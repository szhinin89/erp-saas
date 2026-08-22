using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IInventoryAdjustmentReasonRepository
{
    Task AddAsync(InventoryAdjustmentReason reason, CancellationToken cancellationToken = default);

    Task<InventoryAdjustmentReason?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );

    Task<InventoryAdjustmentReason?> GetByCodeAsync(
        Guid tenantId,
        string code,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<InventoryAdjustmentReason>> ListAsync(
        Guid tenantId,
        Guid? companyId,
        bool includeInactive,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

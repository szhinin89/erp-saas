using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

/// <summary>
/// Motivos de la empresa activa: el filtro global de <c>ICompanyOperationalEntity</c> aplica
/// Tenant+Company fail-closed a toda lectura; ningún método recibe ni acepta otra empresa.
/// </summary>
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
        bool includeInactive,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

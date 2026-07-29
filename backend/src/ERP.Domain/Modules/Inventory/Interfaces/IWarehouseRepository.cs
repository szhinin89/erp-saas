using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IWarehouseRepository
{
    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken = default);
    Task<Warehouse?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );

    /// <summary>Verifica si ya existe una bodega con el mismo código dentro de la misma sucursal.</summary>
    Task<bool> ExistsCodeAsync(
        Guid tenantId,
        Guid branchId,
        string code,
        Guid? excludeId,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<Warehouse>> GetAsync(
        Guid tenantId,
        bool? activeFilter,
        string? search,
        Guid? branchId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

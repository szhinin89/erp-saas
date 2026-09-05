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

    /// <summary>
    /// ZH-AUTH-MASTERDATA-REPOSITORY-COMPANY-SCOPE-07A — defensa en profundidad explícita: valida
    /// <paramref name="companyId"/> en el propio predicado, además del filtro global de EF
    /// (<c>ICompanyOperationalEntity</c>, ya fail-closed vía <c>ForOperationalScope</c>). No
    /// reemplaza las validaciones de sucursal agregadas en ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06
    /// (Enable/DisableWarehouseCommandHandler siguen comparando <c>BranchId</c> tras esta llamada).
    /// </summary>
    Task<Warehouse?> GetByIdForCompanyAsync(
        Guid tenantId,
        Guid companyId,
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

    /// <summary>
    /// CONFIG-FOUNDATION-P0-01: resuelve la bodega principal (<see cref="Warehouse.IsMain"/>)
    /// activa de una sucursal — fuente de fallback del default de bodega de venta cuando no hay
    /// OrgSetting de sucursal configurado. Null si la sucursal no tiene bodega principal activa.
    /// </summary>
    Task<Warehouse?> GetMainForBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

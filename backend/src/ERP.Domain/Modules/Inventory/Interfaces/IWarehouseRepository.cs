using ERP.Domain.Modules.Inventory.Entities;
using ERP.Domain.Modules.Inventory.Enums;

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

    /// <summary>
    /// WAREHOUSE-BRANCH-REASSIGNMENT-INTEGRITY-01: bloquea la fila de la bodega (<c>FOR UPDATE</c>,
    /// requiere transacción activa) y la recarga. Serializa el cambio de sucursal con el registro de
    /// Kardex, que lee la sucursal de la bodega con <c>FOR SHARE</c> en su transacción.
    /// </summary>
    Task<Warehouse?> GetByIdForCompanyForUpdateAsync(
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

    /// <summary>
    /// WAREHOUSE-BRANCH-REASSIGNMENT-INTEGRITY-01: primer motivo por el que la bodega no puede
    /// cambiar de sucursal (existencias distintas de cero, kardex, lotes/series/reservas, documentos
    /// o asignaciones de costo, configuración predeterminada o caja). No evalúa
    /// <see cref="Warehouse.IsMain"/>; eso lo resuelve el handler con la entidad cargada.
    /// </summary>
    Task<WarehouseBranchChangeBlocker> GetBranchChangeBlockerAsync(
        Guid tenantId,
        Guid companyId,
        Guid warehouseId,
        CancellationToken cancellationToken = default
    );

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

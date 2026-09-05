using ERP.Domain.Modules.Inventory.Entities;

namespace ERP.Domain.Modules.Inventory.Interfaces;

public interface IStockAdjustmentRepository
{
    Task AddAsync(StockAdjustment adjustment, CancellationToken cancellationToken = default);
    Task<StockAdjustment?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default
    );
    Task<int> GetNextSequentialAsync(Guid tenantId, CancellationToken cancellationToken = default);
    /// <summary>
    /// ZH-AUTH-INVENTORY-BRANCH-READ-SCOPE-06 — <paramref name="branchWarehouseIds"/> restringe el
    /// listado a las bodegas de la sucursal activa cuando el caller no filtró por una bodega
    /// específica (<paramref name="warehouseId"/> ya viene validado contra esa misma sucursal por
    /// el handler antes de llegar aquí). Null = sin restricción adicional (uso interno/legacy).
    /// </summary>
    Task<(IReadOnlyList<StockAdjustment> Items, int TotalCount)> GetPagedAsync(
        Guid tenantId,
        int pageNumber,
        int pageSize,
        Guid? warehouseId,
        string? status,
        Guid? reasonId,
        string? movementType,
        DateTime? startDate,
        DateTime? endDate,
        IReadOnlyCollection<Guid>? branchWarehouseIds = null,
        CancellationToken cancellationToken = default
    );
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

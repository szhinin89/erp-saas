using ERP.Domain.Inventario.Entities;

namespace ERP.Domain.Inventario.Interfaces;

public interface IInventarioStockRepository
{
    Task<StockActual?> GetStockByTenantBodegaProductAsync(
        Guid tenantId,
        Guid bodegaId,
        Guid productoId,
        CancellationToken ct = default);

    Task<IReadOnlyList<StockActual>> GetStockByTenantBodegaAsync(
        Guid tenantId,
        Guid bodegaId,
        Guid? productoId,
        CancellationToken ct = default);

    Task AddStockActualAsync(StockActual entity, CancellationToken ct = default);

    Task AddMovimientoAsync(InventarioMovimiento movimiento, CancellationToken ct = default);
}

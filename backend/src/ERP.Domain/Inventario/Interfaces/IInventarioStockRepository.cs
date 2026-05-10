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

    /// <summary>
    /// Descuenta <paramref name="delta"/> unidades de stock de forma atómica:
    /// UPDATE ... WHERE (cantidad - cantidad_reservada) >= delta.
    /// Retorna la cantidad anterior (para registrar en el movimiento).
    /// Retorna null si no hay stock suficiente — puede ser por concurrencia
    /// aunque la pre-verificación haya pasado.
    /// </summary>
    Task<decimal?> DecrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid updatedBy, CancellationToken ct = default);

    /// <summary>
    /// Incrementa stock de forma atómica: UPSERT (crea el registro si no existe).
    /// Retorna la cantidad anterior (0 si era la primera entrada en esa bodega).
    /// </summary>
    Task<decimal> IncrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid createdBy, CancellationToken ct = default);
}

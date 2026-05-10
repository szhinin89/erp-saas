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
    /// También actualiza <c>valor_total_stock</c> usando <paramref name="costoUnitario"/>
    /// para mantener el costo promedio actualizado.
    /// Retorna la cantidad anterior (para registrar en el movimiento).
    /// Retorna null si no hay stock suficiente.
    /// </summary>
    Task<decimal?> DecrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid updatedBy,
        CancellationToken ct = default,
        decimal costoUnitario = 0m);

    /// <summary>
    /// Incrementa stock de forma atómica: UPSERT (crea el registro si no existe).
    /// También actualiza <c>valor_total_stock</c> usando <paramref name="costoUnitario"/>.
    /// Retorna la cantidad anterior (0 si era la primera entrada en esa bodega).
    /// </summary>
    Task<decimal> IncrementarStockAtomicoAsync(
        Guid tenantId, Guid bodegaId, Guid productoId,
        decimal delta, Guid createdBy,
        CancellationToken ct = default,
        decimal costoUnitario = 0m);

    /// <summary>
    /// Retorna movimientos de un producto en una bodega, ordenados por fecha de creación.
    /// Ambos extremos de fecha son inclusivos. Si son null no se aplica ese filtro.
    /// </summary>
    Task<IReadOnlyList<InventarioMovimiento>> GetMovimientosAsync(
        Guid      tenantId,
        Guid      productoId,
        Guid      bodegaId,
        DateTime? desdeUtc,
        DateTime? hastaUtc,
        CancellationToken ct = default);
}

namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Proporciona el costo promedio ponderado actual de un producto en una Warehouse.
/// Se usa en movimientos de salida (ventas, transferencias, ajustes negativos)
/// para valorizar el movimiento al costo correcto.
/// </summary>
public interface IAverageCostService
{
    /// <summary>
    /// Retorna el costo unitario promedio actual del producto en la Warehouse.
    /// Lee directamente ValorTotalStock / Cantidad desde CurrentStock.
    /// Retorna 0 si no hay stock registrado o la cantidad es cero.
    /// </summary>
    Task<decimal> ObtenerCostoPromedioAsync(
        Guid subscriberId,
        Guid productoId,
        Guid bodegaId,
        CancellationToken ct = default);
}

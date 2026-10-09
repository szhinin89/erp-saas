namespace ERP.Application.Modules.InitialLoad.Interfaces;

/// <summary>
/// IL-4A — lectura de solo consulta del inventario existente para la Carga Inicial: el saldo de
/// apertura solo se admite sobre un Item+Bodega sin historia. Cualquier CurrentStock o movimiento de
/// Kardex previo (de cualquier tipo, aunque el saldo actual sea cero) bloquea la fila — se usa un
/// ajuste normal. Company scope por filtros globales fail-closed.
/// </summary>
public interface IInitialStockLookup
{
    Task<bool> HasStockHistoryAsync(Guid itemId, Guid warehouseId, CancellationToken ct);
}

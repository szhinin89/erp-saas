namespace ERP.Application.Common.Interfaces;

/// <summary>
/// Filas agregadas por día desde <c>mv_saldos_diarios</c> (PostgreSQL).
/// </summary>
public sealed record KardexMvDayAggregate(
    DateOnly Fecha,
    decimal EntradasCantidad,
    decimal EntradasValor,
    decimal SalidasCantidad,
    decimal SalidasValor);

/// <summary>
/// Lectura opcional de la vista materializada de saldos diarios.
/// Si la vista no existe o el motor no es compatible, los métodos devuelven <c>null</c>
/// y el kardex debe calcularse solo con <c>inventario_movimientos</c>.
/// </summary>
public interface IKardexMaterializedDailySummariesReader
{
    /// <summary>
    /// Agregados por día en el rango inclusive. <c>null</c> = no usar MV (fallback a movimientos).
    /// </summary>
    Task<IReadOnlyList<KardexMvDayAggregate>?> TryGetDailyAggregatesAsync(
        Guid tenantId,
        Guid productoId,
        Guid bodegaId,
        DateOnly fromInclusive,
        DateOnly toInclusive,
        CancellationToken cancellationToken = default);
}

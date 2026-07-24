namespace ERP.Application.Common.Config;

/// <summary>
/// Opciones de comportamiento del Kardex. Se configuran en appsettings.json
/// bajo la sección "Kardex".
/// </summary>
public sealed class KardexOptions
{
    public const string Section = "Kardex";

    /// <summary>
    /// true  → usa KardexSnapshot como punto de partida (O(período) vs O(total)).
    /// false → calcula siempre desde el origen (comportamiento original, sin dependencia de snapshots).
    ///
    /// note: el modo escalable no se activa por defecto. En producción se debe
    /// habilitar manualmente cuando el volumen de datos lo requiera.
    /// </summary>
    public bool UseScalableMode { get; set; }

    /// <summary>
    /// Días máximos de rango para procesamiento síncrono (en modo escalable).
    /// Si el rango supera este valor y <see cref="EnableAsyncReport"/> = true,
    /// el endpoint retorna 202 Accepted con un jobId en lugar del resultado inmediato.
    /// </summary>
    public int MaxDaysForSync { get; set; } = 90;

    /// <summary>
    /// Umbral de movimientos para cálculo síncrono. Si el número estimado de movimientos
    /// en el rango supera este valor se redirige a procesamiento asíncrono.
    /// </summary>
    public int MaxMovementsForSync { get; set; } = 200_000;

    /// <summary>
    /// Habilita el modo de reporte asíncrono. Si false, el endpoint siempre responde
    /// de forma síncrona (aunque el rango sea largo), simplemente puede tardar más.
    /// </summary>
    public bool EnableAsyncReport { get; set; } = true;

    /// <summary>
    /// Si <c>true</c> (junto con <see cref="UseScalableMode"/> y snapshot), el hueco entre el snapshot y el
    /// inicio del período usa días UTC completos agregados en <c>mv_saldos_diarios</c>: por cada día se aplica
    /// primero el total de entradas del día y luego el de salidas (aproximación frente al orden real intra-día
    /// del promedio ponderado móvil). Los tramos parcial del primer y último día siguen siendo movimientos
    /// detallados. Si la MV no está disponible, se vuelve a leer <c>inventario_movimientos</c>.
    /// </summary>
    public bool UseMaterializedDailySummaries { get; set; }
}

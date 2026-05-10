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
    /// </summary>
    public bool UseScalableMode { get; set; } = true;

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
    public int MaxMovimientosSync { get; set; } = 200_000;

    /// <summary>
    /// Habilita el modo de reporte asíncrono. Si false, el endpoint siempre responde
    /// de forma síncrona (aunque el rango sea largo), simplemente puede tardar más.
    /// </summary>
    public bool EnableAsyncReport { get; set; } = true;
}

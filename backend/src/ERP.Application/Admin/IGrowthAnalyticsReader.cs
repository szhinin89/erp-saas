namespace ERP.Application.Admin;

public interface IGrowthAnalyticsReader
{
    /// <summary>
    /// Serie temporal de altas (empresas, usuarios globales, membresías) y acumulados al cierre de cada periodo.
    /// </summary>
    /// <param name="fromUtc">Inicio inclusive (fecha UTC, normalmente 00:00).</param>
    /// <param name="toUtc">Fin inclusive (se interpreta como fin del día UTC si viene sin hora).</param>
    /// <param name="granularity">day, week o month.</param>
    Task<GrowthAnalyticsResponseDto> GetSeriesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mismo rango y buckets que <see cref="GetSeriesAsync"/>; valores en moneda (MRR mensual equivalente por tenant).
    /// </summary>
    Task<GrowthMonetaryResponseDto> GetMonetarySeriesAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string granularity,
        CancellationToken cancellationToken = default);
}

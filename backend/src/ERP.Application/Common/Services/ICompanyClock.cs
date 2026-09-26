namespace ERP.Application.Common.Services;

/// <summary>
/// Fuente única de la fecha calendario "hoy" de una empresa, calculada en su zona horaria
/// (<c>Company.Timezone</c>, p. ej. "America/Guayaquil"), nunca en UTC — y único camino para
/// convertir una hora de pared de la empresa a un instante UTC y viceversa. La aritmética vive en
/// <see cref="CompanyTimeZone"/>; este servicio solo resuelve el Timezone de la empresa.
/// </summary>
/// <remarks>
/// Bug corregido (SRI [65] FECHA EMISIÓN EXTEMPORÁNEA): comparar una fecha de negocio
/// ecuatoriana contra <c>DateTime.UtcNow.Date</c> desplaza el día calendario en operaciones
/// realizadas entre las 19:00 y 23:59 hora Ecuador (UTC-5), porque en ese rango UTC ya cruzó
/// la medianoche del día siguiente. Todo cálculo de "hoy" para reglas de negocio de Ventas/SRI
/// debe pasar por este servicio.
/// </remarks>
public interface ICompanyClock
{
    /// <summary>Fecha calendario "hoy" en la zona horaria de la empresa.</summary>
    Task<DateOnly> TodayAsync(Guid companyId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Rango UTC semiabierto <c>[StartUtc, EndUtc)</c> del día "hoy" de la empresa.</summary>
    Task<(DateTime StartUtc, DateTime EndUtc)> TodayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Rango UTC semiabierto <c>[StartUtc, EndUtc)</c> de un día calendario de la empresa — filtro
    /// "día de empresa" sobre columnas instante (timestamptz). Única implementación.
    /// </summary>
    Task<(DateTime StartUtc, DateTime EndUtc)> DayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        DateOnly day,
        CancellationToken ct = default
    );

    /// <summary>
    /// Fecha calendario, en la zona horaria de la empresa, de un instante UTC arbitrario (pasado o
    /// presente) — a diferencia de <see cref="TodayAsync"/> (siempre "ahora"), usado para
    /// reconstruir/remediar la fecha de negocio de un evento histórico ya ocurrido (ej. un
    /// <c>AuthorizedAtUtc</c> ya persistido) sin re-derivarlo de UTC crudo.
    /// </summary>
    Task<DateOnly> LocalDateAsync(
        Guid companyId,
        Guid tenantId,
        DateTime utcInstant,
        CancellationToken ct = default
    );

    /// <summary>
    /// Hora de pared de la empresa (Kind=Unspecified — p. ej. FECHA_AUTORIZACION del TXT SRI,
    /// expresada en hora Ecuador) → instante UTC real. Nunca <c>DateTime.SpecifyKind(Utc)</c>.
    /// </summary>
    Task<DateTime> CompanyLocalToUtcAsync(
        Guid companyId,
        Guid tenantId,
        DateTime companyLocal,
        CancellationToken ct = default
    );
}

/// <summary>
/// Filtro "rango de días de empresa" sobre columnas instante (timestamptz): <c>[desde, hasta]</c>
/// como fechas de negocio inclusivas → <c>[FromUtc, ToUtcExclusive)</c>. Construido únicamente
/// sobre <see cref="ICompanyClock.DayUtcRangeAsync"/> — sin aritmética propia.
/// </summary>
public static class CompanyClockRangeExtensions
{
    public static async Task<(DateTime? FromUtc, DateTime? ToUtcExclusive)> DaysUtcRangeAsync(
        this ICompanyClock clock,
        Guid companyId,
        Guid tenantId,
        DateOnly? fromDay,
        DateOnly? toDay,
        CancellationToken ct = default
    )
    {
        DateTime? fromUtc = fromDay is DateOnly f
            ? (await clock.DayUtcRangeAsync(companyId, tenantId, f, ct)).StartUtc
            : null;
        DateTime? toUtcExclusive = toDay is DateOnly t
            ? (await clock.DayUtcRangeAsync(companyId, tenantId, t, ct)).EndUtc
            : null;
        return (fromUtc, toUtcExclusive);
    }
}

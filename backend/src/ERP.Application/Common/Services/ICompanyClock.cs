namespace ERP.Application.Common.Services;

/// <summary>
/// Fuente única de la fecha calendario "hoy" de una empresa, calculada en su zona horaria
/// (<c>Company.Timezone</c>, p. ej. "America/Guayaquil"), nunca en UTC.
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

    /// <summary>Rango UTC que cubre el día calendario "hoy" en la zona horaria de la empresa.</summary>
    Task<(DateTime StartUtc, DateTime EndUtc)> TodayUtcRangeAsync(
        Guid companyId,
        Guid tenantId,
        CancellationToken ct = default
    );
}

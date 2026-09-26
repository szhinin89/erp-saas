namespace ERP.Application.Common.Services;

/// <summary>
/// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — única implementación de la aritmética de zona horaria
/// del ERP (<c>Company.Timezone</c> ⇄ UTC). No es una fuente de "hoy" por sí misma: el día operativo
/// de una empresa se pide siempre a <see cref="ICompanyClock"/>, que resuelve el Timezone de la
/// empresa y delega aquí. La única otra consumidora es el catálogo SRI platform-scoped (sin
/// empresa en contexto), que usa la misma matemática con la zona fiscal nacional.
/// </summary>
/// <remarks>
/// Contrato temporal (docs/architecture/data-standards.md § Contrato temporal):
/// <list type="bullet">
/// <item>Fecha de negocio = <see cref="DateOnly"/>; nunca pasa por aquí salvo para derivar "hoy" o un
/// rango UTC de un día de empresa.</item>
/// <item>Instante = <see cref="DateTime"/> Kind=Utc. Una hora de pared (wall clock) ingresada por un
/// usuario o recibida de una fuente sin offset (TXT SRI) es Kind=Unspecified y SOLO se convierte a
/// UTC con <see cref="ToUtc"/> — nunca con <c>DateTime.SpecifyKind</c>.</item>
/// </list>
/// </remarks>
public static class CompanyTimeZone
{
    /// <summary>
    /// Zona fiscal nacional (Ecuador continental, sin horario de verano). Default de
    /// <c>Company.Timezone</c> y zona del catálogo SRI platform-scoped.
    /// </summary>
    public const string DefaultTimezoneId = "America/Guayaquil";

    /// <summary>
    /// Resuelve un identificador IANA; vacío o desconocido por el SO cae a la zona fiscal nacional
    /// con offset fijo UTC-5 (defensivo — Ecuador continental no observa horario de verano).
    /// </summary>
    public static TimeZoneInfo Resolve(string? timezoneId)
    {
        var id = string.IsNullOrWhiteSpace(timezoneId) ? DefaultTimezoneId : timezoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                "Ecuador-Fixed-UTC-5",
                TimeSpan.FromHours(-5),
                "Ecuador (UTC-5)",
                "Ecuador (UTC-5)"
            );
        }
    }

    /// <summary>Fecha calendario "hoy" en <paramref name="tz"/>.</summary>
    public static DateOnly Today(TimeZoneInfo tz) => LocalDate(DateTime.UtcNow, tz);

    /// <summary>Fecha calendario, en <paramref name="tz"/>, del instante UTC dado.</summary>
    public static DateOnly LocalDate(DateTime utcInstant, TimeZoneInfo tz) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(RequireUtc(utcInstant), tz));

    /// <summary>Hora de pared (Kind=Unspecified) en <paramref name="tz"/> del instante UTC dado.</summary>
    public static DateTime ToLocal(DateTime utcInstant, TimeZoneInfo tz) =>
        DateTime.SpecifyKind(
            TimeZoneInfo.ConvertTimeFromUtc(RequireUtc(utcInstant), tz),
            DateTimeKind.Unspecified
        );

    /// <summary>
    /// Hora de pared de la empresa → instante UTC real. La entrada debe ser Kind=Unspecified (una
    /// hora "tal como la ve el usuario"); un Kind=Utc/Local indica que el llamador ya tiene un
    /// instante y confundió la intención — se rechaza en vez de reinterpretarlo.
    /// Hora ambigua (retroceso DST): se toma la ocurrencia en horario estándar (criterio .NET).
    /// Hora inexistente (salto DST): se rechaza — nunca se inventa un instante.
    /// </summary>
    public static DateTime ToUtc(DateTime companyLocal, TimeZoneInfo tz)
    {
        if (companyLocal.Kind != DateTimeKind.Unspecified)
            throw new ArgumentException(
                $"Se esperaba una hora local de empresa (Kind=Unspecified), se recibió Kind={companyLocal.Kind}. "
                    + "Un instante ya UTC no se convierte de nuevo.",
                nameof(companyLocal)
            );
        if (tz.IsInvalidTime(companyLocal))
            throw new ArgumentException(
                $"La hora {companyLocal:yyyy-MM-dd HH:mm} no existe en la zona {tz.Id} (cambio de horario).",
                nameof(companyLocal)
            );
        return TimeZoneInfo.ConvertTimeToUtc(companyLocal, tz);
    }

    /// <summary>
    /// Rango UTC semiabierto <c>[StartUtc, EndUtc)</c> del día calendario <paramref name="day"/> en
    /// <paramref name="tz"/>. Única implementación de "día de empresa → rango UTC".
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayUtcRange(DateOnly day, TimeZoneInfo tz) =>
        (StartOfDayUtc(day, tz), StartOfDayUtc(day.AddDays(1), tz));

    /// <summary>
    /// Instante UTC en que empieza el día <paramref name="day"/> en <paramref name="tz"/>. En zonas
    /// cuyo salto DST ocurre a medianoche, el día empieza en el primer minuto existente.
    /// </summary>
    private static DateTime StartOfDayUtc(DateOnly day, TimeZoneInfo tz)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (tz.IsInvalidTime(local))
            local = local.AddMinutes(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : throw new ArgumentException(
                $"Se esperaba un instante UTC (Kind=Utc), se recibió Kind={value.Kind}.",
                nameof(value)
            );
}

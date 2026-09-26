namespace ERP.Domain.Common;

/// <summary>
/// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — guard de dominio para instantes (timestamptz). Única
/// intención: "este valor YA es un instante con zona conocida". Nunca reinterpreta una hora sin
/// zona: un <see cref="DateTimeKind.Unspecified"/> es una hora de pared (p. ej. un
/// datetime-local sin offset) y etiquetarlo UTC con <c>SpecifyKind</c> corre el instante real
/// ±N horas. Esa conversión tiene un único camino: <c>ICompanyClock.CompanyLocalToUtcAsync</c>
/// (hora de empresa → UTC con <c>Company.Timezone</c>).
/// </summary>
public static class UtcDateTime
{
    /// <summary>
    /// Utc → sin cambios. Local → instante real convertido a UTC (así materializa .NET un ISO con
    /// offset explícito, p. ej. "2026-09-25T14:38:00-05:00"). Unspecified → rechazado
    /// (<see cref="ArgumentException"/>, HTTP 400): el contrato API exige ISO-8601 con "Z" u offset.
    /// </summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException(
            $"Instante sin zona horaria ({value:yyyy-MM-ddTHH:mm:ss}). Enviar ISO-8601 UTC terminado en 'Z' "
                + "(o con offset); una hora local de empresa se convierte vía ICompanyClock.CompanyLocalToUtcAsync."
        ),
    };

    public static DateTime? EnsureUtc(DateTime? value) =>
        value.HasValue ? EnsureUtc(value.Value) : null;

    /// <summary>
    /// Único parser de instantes recibidos por la API (JSON body y query string): ISO-8601 con zona
    /// explícita ("Z" u offset) → Kind=Utc. Sin zona → <c>false</c> (el llamador responde 400);
    /// nunca se asume UTC ni la zona del servidor.
    /// </summary>
    public static bool TryParseInstant(string? text, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (
            !DateTime.TryParse(
                text.Trim(),
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed
            )
            || parsed.Kind == DateTimeKind.Unspecified
        )
            return false;
        utc = EnsureUtc(parsed);
        return true;
    }
}

namespace ERP.Domain.Modules.Retentions;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 (ADR-036 §2, NAC-DGERCGC25-00000014/-00000017) — plazo ORDINARIO para
/// solicitar la anulación de un comprobante de retención: hasta el día 7 del mes siguiente a la fecha
/// de EMISIÓN (fecha de negocio, ADR-034).
///
/// El ERP no tiene un calendario de días hábiles/feriados con fuente oficial: no corre el plazo al
/// siguiente día hábil (inventar feriados está prohibido). La UI muestra esta fecha como ordinaria y
/// advierte que debe validarse contra el SRI. La excepción de ISD no aplica: el dominio no emite
/// retenciones de ISD (<see cref="Enums.RetentionTaxType"/> solo IVA/Renta).
/// </summary>
public static class RetentionAnnulmentDeadline
{
    public const int OrdinaryDayOfFollowingMonth = 7;

    public static DateOnly Ordinary(DateOnly issueDate)
    {
        var firstOfNext = new DateOnly(issueDate.Year, issueDate.Month, 1).AddMonths(1);
        return new DateOnly(firstOfNext.Year, firstOfNext.Month, OrdinaryDayOfFollowingMonth);
    }
}

namespace ERP.Infrastructure.Tests.Common;

/// <summary>
/// <see cref="TimeProvider"/> con un instante fijo: los tests que dependen del "hoy" de la empresa
/// (<c>ICompanyClock</c>) no dependen de la hora real de ejecución.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

/// <summary>
/// ZH-ACCOUNTING-DATE-BOUNDARY-01 — frontera entre el día UTC y el día de la empresa
/// (Company.Timezone = America/Guayaquil, UTC-5, default de <c>Company.CreateManaged</c>).
/// El asiento de un hecho sin fecha propia (autorización de devolución, aplicación/reversa de
/// crédito de proveedor, reversa de cobro) se fecha con el "hoy" de la empresa vía
/// <c>ICompanyClock</c> (ADR-034); el período contable sembrado por el test debe salir de ese
/// mismo día, nunca de <c>DateOnly.FromDateTime(DateTime.UtcNow)</c>.
/// </summary>
public static class AccountingDateBoundary
{
    /// <summary>UTC ya es 1-oct-2026 00:30; en Guayaquil sigue siendo 30-sep-2026 19:30.</summary>
    public static readonly DateTimeOffset UtcInstant = new(2026, 10, 1, 0, 30, 0, TimeSpan.Zero);

    /// <summary>Día de negocio de la empresa en <see cref="UtcInstant"/>.</summary>
    public static readonly DateOnly CompanyToday = new(2026, 9, 30);

    public static TimeProvider Clock => new FixedTimeProvider(UtcInstant);
}

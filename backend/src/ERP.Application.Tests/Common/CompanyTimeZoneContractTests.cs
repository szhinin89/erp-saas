using ERP.Application.Common.Services;
using FluentAssertions;

namespace ERP.Application.Tests.Common;

/// <summary>
/// ZH-TEMPORAL-CONTRACT-SINGLE-SOURCE-02 — contrato de la única aritmética de zona horaria del ERP
/// (<see cref="CompanyTimeZone"/>, en la que delega <see cref="ICompanyClock"/>).
/// INSTANTE: hora de empresa → UTC real una sola vez; UTC → misma hora de empresa; sin drift.
/// FECHA DE NEGOCIO: el día nunca se desplaza por zona horaria.
/// </summary>
public sealed class CompanyTimeZoneContractTests
{
    private static readonly TimeZoneInfo Guayaquil = CompanyTimeZone.Resolve("America/Guayaquil");
    private static readonly TimeZoneInfo NewYork = CompanyTimeZone.Resolve("America/New_York");
    private static readonly TimeZoneInfo Madrid = CompanyTimeZone.Resolve("Europe/Madrid");

    private static DateTime Wall(int y, int mo, int d, int h, int mi) =>
        new(y, mo, d, h, mi, 0, DateTimeKind.Unspecified);

    private static DateTime Utc(int y, int mo, int d, int h, int mi) =>
        new(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    [Fact]
    public void Instante_Guayaquil_1438_es_1938Z_y_vuelve_a_1438()
    {
        var utc = CompanyTimeZone.ToUtc(Wall(2026, 9, 25, 14, 38), Guayaquil);

        utc.Should().Be(Utc(2026, 9, 25, 19, 38));
        utc.Kind.Should().Be(DateTimeKind.Utc);
        CompanyTimeZone.ToLocal(utc, Guayaquil).Should().Be(Wall(2026, 9, 25, 14, 38));
        CompanyTimeZone.LocalDate(utc, Guayaquil).Should().Be(new DateOnly(2026, 9, 25));
    }

    [Fact]
    public void Editar_guardar_100_veces_no_acumula_drift()
    {
        var stored = Utc(2026, 9, 25, 19, 38);
        for (var i = 0; i < 100; i++)
            stored = CompanyTimeZone.ToUtc(CompanyTimeZone.ToLocal(stored, Guayaquil), Guayaquil);

        stored.Should().Be(Utc(2026, 9, 25, 19, 38));
    }

    [Theory]
    [InlineData(23, 59)] // 18:59 Ecuador del 25
    [InlineData(0, 0)] //   19:00 Ecuador del 25 (UTC ya es 26)
    [InlineData(4, 59)] //  23:59 Ecuador del 25 (UTC ya es 26)
    public void Dia_de_empresa_Ecuador_entre_1859_y_2359_sigue_siendo_25(int utcHour, int utcMinute)
    {
        var utcDay = utcHour == 23 ? 25 : 26;
        CompanyTimeZone
            .LocalDate(Utc(2026, 9, utcDay, utcHour, utcMinute), Guayaquil)
            .Should()
            .Be(new DateOnly(2026, 9, 25));
    }

    [Fact]
    public void Hora_sin_zona_nunca_se_reetiqueta_Un_instante_UTC_no_se_convierte_de_nuevo()
    {
        var act = () => CompanyTimeZone.ToUtc(Utc(2026, 9, 25, 19, 38), Guayaquil);
        act.Should().Throw<ArgumentException>();

        var local = () => CompanyTimeZone.LocalDate(Wall(2026, 9, 25, 14, 38), Guayaquil);
        local.Should().Throw<ArgumentException>("un DateTime Unspecified no es un instante");
    }

    [Fact]
    public void DST_New_York_verano_invierno_salto_y_retroceso()
    {
        CompanyTimeZone.ToUtc(Wall(2026, 7, 1, 10, 0), NewYork).Should().Be(Utc(2026, 7, 1, 14, 0));
        CompanyTimeZone.ToUtc(Wall(2026, 12, 1, 10, 0), NewYork).Should().Be(Utc(2026, 12, 1, 15, 0));

        // Salto de primavera (08/03/2026 02:00 → 03:00): 02:30 no existe — se rechaza.
        var gap = () => CompanyTimeZone.ToUtc(Wall(2026, 3, 8, 2, 30), NewYork);
        gap.Should().Throw<ArgumentException>();

        // Retroceso de otoño (01/11/2026): 01:30 ocurre dos veces — horario estándar (EST, UTC-5).
        CompanyTimeZone.ToUtc(Wall(2026, 11, 1, 1, 30), NewYork).Should().Be(Utc(2026, 11, 1, 6, 30));
    }

    [Fact]
    public void DST_Madrid_verano_round_trip()
    {
        var utc = CompanyTimeZone.ToUtc(Wall(2026, 7, 1, 10, 0), Madrid);
        utc.Should().Be(Utc(2026, 7, 1, 8, 0));
        CompanyTimeZone.ToLocal(utc, Madrid).Should().Be(Wall(2026, 7, 1, 10, 0));
    }

    [Fact]
    public void DayUtcRange_es_semiabierto_y_respeta_Company_Timezone()
    {
        CompanyTimeZone
            .DayUtcRange(new DateOnly(2026, 9, 25), Guayaquil)
            .Should()
            .Be((Utc(2026, 9, 25, 5, 0), Utc(2026, 9, 26, 5, 0)));

        // Día de cambio DST en New York: 23 horas.
        var (start, end) = CompanyTimeZone.DayUtcRange(new DateOnly(2026, 3, 8), NewYork);
        start.Should().Be(Utc(2026, 3, 8, 5, 0));
        end.Should().Be(Utc(2026, 3, 9, 4, 0));
        (end - start).Should().Be(TimeSpan.FromHours(23));
    }

    [Theory]
    [InlineData("America/Guayaquil")]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Europe/Madrid")]
    public void Fecha_de_negocio_DateOnly_es_invariante_a_la_zona(string timezoneId)
    {
        var tz = CompanyTimeZone.Resolve(timezoneId);
        var day = new DateOnly(2026, 9, 25);

        // El día de negocio define su propio rango en la zona de la empresa y ese rango vuelve
        // exactamente al mismo día — nunca 24/09 ni 26/09.
        var (start, end) = CompanyTimeZone.DayUtcRange(day, tz);
        CompanyTimeZone.LocalDate(start, tz).Should().Be(day);
        CompanyTimeZone.LocalDate(end.AddTicks(-1), tz).Should().Be(day);
        day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).Should().Be("2026-09-25");
        day.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture).Should().Be("25/09/2026");
    }

    [Fact]
    public void Zona_vacia_o_desconocida_cae_a_la_zona_fiscal_nacional()
    {
        CompanyTimeZone.ToUtc(Wall(2026, 9, 25, 14, 38), CompanyTimeZone.Resolve(null))
            .Should()
            .Be(Utc(2026, 9, 25, 19, 38));
        CompanyTimeZone.ToUtc(Wall(2026, 9, 25, 14, 38), CompanyTimeZone.Resolve("Zona/Inexistente"))
            .Should()
            .Be(Utc(2026, 9, 25, 19, 38));
    }
}

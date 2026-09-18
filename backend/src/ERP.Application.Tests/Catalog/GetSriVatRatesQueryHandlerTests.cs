using ERP.Application.Modules.Catalog.UseCases;
using ERP.Domain.Modules.SriCatalogs.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Catalog;

/// <summary>
/// AUDIT-DATETIME-COMPANY-CLOCK-01 / DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01 — la vigencia de tasas
/// IVA es un catálogo SRI platform-scoped (sin tenant/empresa en contexto, ver el comentario de
/// <c>GetSriVatRatesQuery</c>), así que no puede resolver <c>ICompanyClock</c> por empresa. El
/// catálogo tributario SRI es nacional (Ecuador, America/Guayaquil) para todas las empresas del
/// sistema — la fecha de vigencia debe evaluarse contra el día calendario ecuatoriano, nunca
/// <c>DateOnly.FromDateTime(DateTime.UtcNow)</c> (que se adelanta un día entre las 19:00 y 23:59
/// hora Ecuador). No se puede inyectar un reloj fijo aquí (SriCatalogClock es un helper interno
/// sin abstracción) — el test valida la conversión real contra un cálculo independiente con el
/// mismo IANA id, tolerando el margen de milisegundos entre ambas lecturas de "ahora".
/// </summary>
public sealed class GetSriVatRatesQueryHandlerTests
{
    [Fact]
    public async Task Usa_el_dia_calendario_de_Ecuador_no_UTC_crudo()
    {
        var repo = new Mock<ISriCatalogLookupRepository>();
        DateOnly? capturedToday = null;
        repo.Setup(r => r.GetActiveVatRatesAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .Callback<DateOnly, CancellationToken>((today, _) => capturedToday = today)
            .ReturnsAsync(Array.Empty<ERP.Domain.Modules.SriCatalogs.Entities.SriVatRate>());

        var handler = new GetSriVatRatesQueryHandler(repo.Object);
        var result = await handler.Handle(new GetSriVatRatesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        capturedToday.Should().NotBeNull();

        var ecuadorTz = ResolveEcuadorTimeZone();
        var expectedToday = DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ecuadorTz)
        );
        capturedToday!.Value.Should().Be(expectedToday);
    }

    private static TimeZoneInfo ResolveEcuadorTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Guayaquil");
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
}

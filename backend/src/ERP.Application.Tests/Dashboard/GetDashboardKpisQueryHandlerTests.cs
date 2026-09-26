using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Application.Modules.Dashboard;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Dashboard;

/// <summary>
/// PRICE-LIST-COMPANY-CLOCK-01 / DATETIME-COMPANY-CLOCK-GLOBAL-FIX-01 — el default de "hoy" para
/// <c>GetDashboardKpisQuery.AsOf</c> venía de <c>DateTime.UtcNow</c> crudo. Repro real: Ecuador
/// (UTC-5) 17/09 19:53 → UTC ya es 18/09 00:53 — el dashboard debe seguir mostrando KPIs del 17/09
/// (el día operativo real de la empresa), no adelantarse un día.
/// </summary>
public sealed class GetDashboardKpisQueryHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static DashboardKpisDto SampleDto(DateOnly asOf) =>
        new(0m, 0, 0m, 0m, 0, 0m, 0, 0m, 0, 0m, 0, 0, 0, asOf, asOf.Month, asOf.Year);

    [Fact]
    public async Task Sin_AsOf_usa_el_dia_operativo_de_la_empresa_nunca_UtcNow_crudo()
    {
        var companyToday = new DateOnly(2026, 9, 17);
        var reader = new Mock<IDashboardKpiReader>();
        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
        var companyClock = new Mock<ICompanyClock>();
        companyClock
            .Setup(c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyToday);

        DateOnly? capturedAsOf = null;
        reader
            .Setup(r =>
                r.ReadAsync(TenantId, CompanyId, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>())
            )
            .Callback<Guid, Guid, DateOnly, CancellationToken>((_, _, asOf, _) => capturedAsOf = asOf)
            .ReturnsAsync((Guid _, Guid _, DateOnly asOf, CancellationToken _) => SampleDto(asOf));

        var handler = new GetDashboardKpisQueryHandler(reader.Object, tenant, company, companyClock.Object);

        var result = await handler.Handle(new GetDashboardKpisQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        capturedAsOf.Should().Be(companyToday);
        companyClock.Verify(
            c => c.TodayAsync(CompanyId, TenantId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Con_AsOf_explicito_no_consulta_ICompanyClock()
    {
        var explicitAsOf = new DateOnly(2026, 3, 1);
        var reader = new Mock<IDashboardKpiReader>();
        reader
            .Setup(r =>
                r.ReadAsync(TenantId, CompanyId, explicitAsOf, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(SampleDto(explicitAsOf));
        var tenant = Mock.Of<ICurrentTenant>(t => t.TenantId == TenantId);
        var company = Mock.Of<ICurrentCompany>(c => c.CompanyId == CompanyId);
        var companyClock = new Mock<ICompanyClock>();

        var handler = new GetDashboardKpisQueryHandler(reader.Object, tenant, company, companyClock.Object);

        var result = await handler.Handle(
            new GetDashboardKpisQuery(explicitAsOf),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue(result.Error);
        companyClock.Verify(
            c => c.TodayAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}

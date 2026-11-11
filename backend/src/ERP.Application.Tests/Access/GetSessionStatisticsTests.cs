using ERP.Application.Access.UseCases.GetSessionStatistics;
using ERP.Application.Common;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

public sealed class GetSessionStatisticsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();

    private static (Mock<ICurrentTenant> tenant, Mock<ICurrentCompany> company) Context(
        bool hasCompany = true
    )
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        var company = new Mock<ICurrentCompany>();
        company.Setup(c => c.HasCompanyContext).Returns(hasCompany);
        company.Setup(c => c.CompanyId).Returns(hasCompany ? CompanyId : Guid.Empty);
        return (tenant, company);
    }

    [Fact]
    public async Task Calcula_las_estadisticas_correctamente_desde_los_conteos_por_status()
    {
        var repo = new Mock<IUserSessionRepository>();
        var (tenant, company) = Context();

        repo.Setup(r => r.GetStatusCountsAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<UserSessionStatus, int>
                {
                    [UserSessionStatus.Active] = 5,
                    [UserSessionStatus.ClosedManually] = 3,
                    [UserSessionStatus.ClosedByNewLogin] = 2,
                    [UserSessionStatus.Expired] = 1,
                }
            );

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object, company.Object);
        var result = await handler.Handle(new GetSessionStatisticsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Active.Should().Be(5);
        result.Value.ClosedManually.Should().Be(3);
        result.Value.ClosedByNewLogin.Should().Be(2);
        result.Value.Expired.Should().Be(1);
        result.Value.Total.Should().Be(11);
    }

    [Fact]
    public async Task Status_ausentes_en_el_diccionario_se_cuentan_como_cero()
    {
        var repo = new Mock<IUserSessionRepository>();
        var (tenant, company) = Context();

        repo.Setup(r => r.GetStatusCountsAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<UserSessionStatus, int>());

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object, company.Object);
        var result = await handler.Handle(new GetSessionStatisticsQuery(), CancellationToken.None);

        result.Value!.Total.Should().Be(0);
    }

    [Fact]
    public async Task IAM_V1_F1_un_CompanyId_de_otra_empresa_no_amplia_el_alcance()
    {
        var otherCompanyId = Guid.NewGuid();
        var repo = new Mock<IUserSessionRepository>();
        var (tenant, company) = Context();
        repo.Setup(r => r.GetStatusCountsAsync(TenantId, CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<UserSessionStatus, int>());

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object, company.Object);
        await handler.Handle(new GetSessionStatisticsQuery(otherCompanyId), CancellationToken.None);

        repo.VerifyAll();
        repo.Verify(
            r => r.GetStatusCountsAsync(TenantId, otherCompanyId, It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task IAM_V1_F1_sin_empresa_activa_es_Forbidden()
    {
        var repo = new Mock<IUserSessionRepository>();
        var (tenant, company) = Context(hasCompany: false);

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object, company.Object);
        var result = await handler.Handle(new GetSessionStatisticsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        repo.VerifyNoOtherCalls();
    }
}

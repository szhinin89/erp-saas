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

    [Fact]
    public async Task Calcula_las_estadisticas_correctamente_desde_los_conteos_por_status()
    {
        var repo = new Mock<IUserSessionRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        repo.Setup(r => r.GetStatusCountsAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Dictionary<UserSessionStatus, int>
                {
                    [UserSessionStatus.Active] = 5,
                    [UserSessionStatus.ClosedManually] = 3,
                    [UserSessionStatus.ClosedByNewLogin] = 2,
                    [UserSessionStatus.Expired] = 1,
                }
            );

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object);
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
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        repo.Setup(r => r.GetStatusCountsAsync(TenantId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<UserSessionStatus, int>());

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object);
        var result = await handler.Handle(new GetSessionStatisticsQuery(), CancellationToken.None);

        result.Value!.Total.Should().Be(0);
    }

    [Fact]
    public async Task Filtra_por_CompanyId_cuando_se_recibe()
    {
        var companyId = Guid.NewGuid();
        var repo = new Mock<IUserSessionRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        repo.Setup(r => r.GetStatusCountsAsync(TenantId, companyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<UserSessionStatus, int>());

        var handler = new GetSessionStatisticsHandler(repo.Object, tenant.Object);
        await handler.Handle(new GetSessionStatisticsQuery(companyId), CancellationToken.None);

        repo.VerifyAll();
    }
}

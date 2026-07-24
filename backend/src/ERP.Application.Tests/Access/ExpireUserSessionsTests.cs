using ERP.Application.Access.UseCases.ExpireUserSessions;
using ERP.Application.Common.Config;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

public sealed class ExpireUserSessionsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid IdentityUserId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private static SessionExpirationOptions Options(int maxDays = 30) => new() { MaxSessionAgeDays = maxDays };

    [Fact]
    public async Task Sesion_activa_no_expirada_permanece_no_se_toca()
    {
        var repo = new Mock<IUserSessionRepository>();
        repo.Setup(r => r.GetExpiredActiveSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSession>());

        var handler = new ExpireUserSessionsHandler(repo.Object, Options());
        var result = await handler.Handle(new ExpireUserSessionsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        repo.Verify(r => r.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sesion_candidata_cambia_a_Expired_y_se_persiste()
    {
        var session = UserSession.Create(TenantId, CompanyId, IdentityUserId, BranchId, "device-1");

        var repo = new Mock<IUserSessionRepository>();
        repo.Setup(r => r.GetExpiredActiveSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { session });

        var handler = new ExpireUserSessionsHandler(repo.Object, Options());
        var result = await handler.Handle(new ExpireUserSessionsCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        session.Status.Should().Be(UserSessionStatus.Expired);
        repo.Verify(r => r.UpdateAsync(session, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Usa_el_cutoff_derivado_de_MaxSessionAgeDays()
    {
        DateTime? capturedCutoff = null;
        var repo = new Mock<IUserSessionRepository>();
        repo.Setup(r => r.GetExpiredActiveSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<DateTime, CancellationToken>((cutoff, _) => capturedCutoff = cutoff)
            .ReturnsAsync(Array.Empty<UserSession>());

        var handler = new ExpireUserSessionsHandler(repo.Object, Options(maxDays: 15));
        await handler.Handle(new ExpireUserSessionsCommand(), CancellationToken.None);

        capturedCutoff.Should().NotBeNull();
        capturedCutoff!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(-15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Multiples_sesiones_de_distintas_empresas_se_expiran_todas_sin_distincion()
    {
        var otherCompanyId = Guid.NewGuid();
        var sessionA = UserSession.Create(TenantId, CompanyId, IdentityUserId, BranchId, "device-1");
        var sessionB = UserSession.Create(TenantId, otherCompanyId, IdentityUserId, BranchId, "device-2");

        var repo = new Mock<IUserSessionRepository>();
        repo.Setup(r => r.GetExpiredActiveSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sessionA, sessionB });

        var handler = new ExpireUserSessionsHandler(repo.Object, Options());
        var result = await handler.Handle(new ExpireUserSessionsCommand(), CancellationToken.None);

        result.Value.Should().Be(2);
        sessionA.Status.Should().Be(UserSessionStatus.Expired);
        sessionB.Status.Should().Be(UserSessionStatus.Expired);
    }

    [Fact]
    public async Task Ejecucion_idempotente_segunda_corrida_no_encuentra_candidatos_ni_falla()
    {
        var session = UserSession.Create(TenantId, CompanyId, IdentityUserId, BranchId, "device-1");
        var repo = new Mock<IUserSessionRepository>();

        var call = 0;
        repo.Setup(r => r.GetExpiredActiveSessionsAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => call++ == 0 ? new[] { session } : Array.Empty<UserSession>());

        var handler = new ExpireUserSessionsHandler(repo.Object, Options());

        var first = await handler.Handle(new ExpireUserSessionsCommand(), CancellationToken.None);
        var second = await handler.Handle(new ExpireUserSessionsCommand(), CancellationToken.None);

        first.Value.Should().Be(1);
        second.Value.Should().Be(0);
        session.Status.Should().Be(UserSessionStatus.Expired);
    }
}

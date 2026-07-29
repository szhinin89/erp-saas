using ERP.Application.Auth.UseCases.Logout;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// Fase E: antes de este fix, LogoutHandler solo revocaba el refresh token y nunca cerraba la
/// UserSession asociada — quedaba "Active" en el dashboard administrativo hasta expirar por el
/// job de Hangfire. Estos tests cubren la sincronización nueva, reutilizando
/// UserSession.CloseManually (mismo método de dominio que CloseUserSessionAdminHandler).
/// </summary>
public sealed class LogoutHandlerTests
{
    private static UserSession NewSession(Guid tenantId, Guid userId, Guid? refreshTokenId) =>
        UserSession.Create(
            tenantId,
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            "POS-1",
            refreshTokenId
        );

    private sealed class Fixture
    {
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<IUserSessionRepository> UserSessionRepository { get; } = new();

        public LogoutHandler BuildHandler() =>
            new(RefreshTokenService.Object, UserSessionRepository.Object);
    }

    [Fact]
    public async Task Sin_refresh_token_falla_sin_tocar_sesiones()
    {
        var f = new Fixture();
        var handler = f.BuildHandler();

        var result = await handler.Handle(
            new LogoutCommand("", AllDevices: false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.UserSessionRepository.Verify(
            r => r.GetByRefreshTokenIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Logout_de_un_dispositivo_cierra_la_UserSession_vinculada_al_RefreshToken_revocado()
    {
        var f = new Fixture();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var refreshTokenId = Guid.NewGuid();
        var session = NewSession(tenantId, userId, refreshTokenId);

        f.RefreshTokenService.Setup(s =>
                s.RevokeAsync("raw-token", "Logout", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(refreshTokenId);
        f.UserSessionRepository.Setup(r =>
                r.GetByRefreshTokenIdAsync(refreshTokenId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(session);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LogoutCommand("raw-token", AllDevices: false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(Domain.Access.Enums.UserSessionStatus.ClosedManually);
        f.UserSessionRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Logout_de_un_dispositivo_sin_UserSession_asociada_no_falla_ni_escribe()
    {
        var f = new Fixture();
        var refreshTokenId = Guid.NewGuid();

        f.RefreshTokenService.Setup(s =>
                s.RevokeAsync(It.IsAny<string>(), "Logout", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(refreshTokenId);
        f.UserSessionRepository.Setup(r =>
                r.GetByRefreshTokenIdAsync(refreshTokenId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((UserSession?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LogoutCommand("raw-token", AllDevices: false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.UserSessionRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Logout_de_un_dispositivo_con_token_desconocido_no_falla_ni_busca_sesion()
    {
        var f = new Fixture();
        f.RefreshTokenService.Setup(s =>
                s.RevokeAsync(It.IsAny<string>(), "Logout", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Guid?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LogoutCommand("raw-token", AllDevices: false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.UserSessionRepository.Verify(
            r => r.GetByRefreshTokenIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Logout_global_cierra_todas_las_sesiones_activas_del_usuario_en_el_tenant()
    {
        var f = new Fixture();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionA = NewSession(tenantId, userId, Guid.NewGuid());
        var sessionB = NewSession(tenantId, userId, Guid.NewGuid());

        f.RefreshTokenService.Setup(s =>
                s.ValidateAndRotateAsync("raw-token", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                RefreshTokenValidationResult.Ok(
                    userId,
                    tenantId,
                    null,
                    RefreshUserType.Identity,
                    "new-token",
                    DateTime.UtcNow.AddDays(1)
                )
            );
        f.UserSessionRepository.Setup(r =>
                r.GetActiveSessionsAsync(userId, tenantId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(new[] { sessionA, sessionB });

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LogoutCommand("raw-token", AllDevices: true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        sessionA.Status.Should().Be(Domain.Access.Enums.UserSessionStatus.ClosedManually);
        sessionB.Status.Should().Be(Domain.Access.Enums.UserSessionStatus.ClosedManually);
        f.RefreshTokenService.Verify(
            s =>
                s.RevokeAllForUserAsync(
                    userId,
                    tenantId,
                    "Logout global",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.UserSessionRepository.Verify(
            r => r.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Logout_global_con_token_invalido_falla_y_no_toca_sesiones()
    {
        var f = new Fixture();
        f.RefreshTokenService.Setup(s =>
                s.ValidateAndRotateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(RefreshTokenValidationResult.Fail("Refresh token inválido."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LogoutCommand("raw-token", AllDevices: true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        f.UserSessionRepository.Verify(
            r =>
                r.GetActiveSessionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}

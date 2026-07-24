using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using ERP.Infrastructure.Services;
using FluentAssertions;
using Moq;

namespace ERP.Infrastructure.Tests.Services;

/// <summary>
/// Punto único de revocación de acceso (refresh tokens + UserSession activas), compuesto a partir
/// de IRefreshTokenService.RevokeAllForUserAsync (ya probado en RefreshTokenServiceTests) y
/// UserSession.CloseManually (dominio, ya probado en UserSessionTests) — este suite solo verifica
/// la composición y el actor pasado a cada pieza, no reimplementa ninguna regla de negocio.
/// </summary>
public sealed class UserAccessRevocationServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();
    private const string Reason = "Contraseña temporal asignada por administrador";

    private static UserSession NewActiveSession() =>
        UserSession.Create(TenantId, Guid.NewGuid(), UserId, Guid.NewGuid(), "device-1");

    private sealed class Fixture
    {
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<IUserSessionRepository> UserSessionRepo { get; } = new();

        public UserAccessRevocationService Build() => new(RefreshTokenService.Object, UserSessionRepo.Object);
    }

    [Fact]
    public async Task Revoca_todos_los_RefreshTokens_activos_del_usuario()
    {
        var f = new Fixture();
        f.UserSessionRepo.Setup(r => r.GetActiveSessionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSession>());

        var service = f.Build();
        await service.RevokeAllAccessAsync(UserId, TenantId, ActorId, Reason, CancellationToken.None);

        f.RefreshTokenService.Verify(
            s => s.RevokeAllForUserAsync(UserId, TenantId, Reason, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cierra_todas_las_UserSessions_activas_con_el_actor_correcto()
    {
        var session1 = NewActiveSession();
        var session2 = NewActiveSession();
        var f = new Fixture();
        f.UserSessionRepo.Setup(r => r.GetActiveSessionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { session1, session2 });

        var service = f.Build();
        await service.RevokeAllAccessAsync(UserId, TenantId, ActorId, Reason, CancellationToken.None);

        session1.Status.Should().Be(UserSessionStatus.ClosedManually);
        session1.UpdatedBy.Should().Be(ActorId);
        session2.Status.Should().Be(UserSessionStatus.ClosedManually);
        session2.UpdatedBy.Should().Be(ActorId);
        f.UserSessionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_sesiones_activas_no_llama_SaveChangesAsync()
    {
        var f = new Fixture();
        f.UserSessionRepo.Setup(r => r.GetActiveSessionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserSession>());

        var service = f.Build();
        await service.RevokeAllAccessAsync(UserId, TenantId, ActorId, Reason, CancellationToken.None);

        f.UserSessionRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Es_idempotente_sobre_una_sesion_ya_cerrada()
    {
        var alreadyClosed = NewActiveSession();
        alreadyClosed.CloseManually(Guid.NewGuid());
        var closedAtBefore = alreadyClosed.ClosedAt;

        var f = new Fixture();
        f.UserSessionRepo.Setup(r => r.GetActiveSessionsAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { alreadyClosed });

        var service = f.Build();
        var act = () => service.RevokeAllAccessAsync(UserId, TenantId, ActorId, Reason, CancellationToken.None);

        await act.Should().NotThrowAsync();
        alreadyClosed.ClosedAt.Should().Be(closedAtBefore, "CloseManually es idempotente — no debe reabrir ni recalcular el cierre");
    }
}

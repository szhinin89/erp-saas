using ERP.Application.Access.UseCases.CloseUserSessionAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Enums;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

public sealed class CloseUserSessionAdminValidatorTests
{
    private static readonly CloseUserSessionAdminValidator Validator = new();

    [Fact]
    public void Command_valido_no_tiene_errores()
    {
        Validator
            .Validate(new CloseUserSessionAdminCommand(Guid.NewGuid()))
            .IsValid.Should()
            .BeTrue();
    }

    [Fact]
    public void SessionId_vacio_es_invalido()
    {
        var result = Validator.Validate(new CloseUserSessionAdminCommand(Guid.Empty));
        result
            .Errors.Should()
            .Contain(e => e.PropertyName == nameof(CloseUserSessionAdminCommand.SessionId));
    }
}

public sealed class CloseUserSessionAdminHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();

    private static (Mock<IUserSessionRepository> repo, Mock<ICurrentUser> user) BuildMocks()
    {
        var repo = new Mock<IUserSessionRepository>();
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(AdminUserId);
        return (repo, user);
    }

    [Fact]
    public async Task Cierra_la_sesion_y_registra_al_admin_como_actor()
    {
        var (repo, user) = BuildMocks();
        var session = UserSession.Create(TenantId, CompanyId, OwnerUserId, BranchId, "device-1");
        repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new CloseUserSessionAdminHandler(repo.Object, user.Object);
        var result = await handler.Handle(
            new CloseUserSessionAdminCommand(session.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(UserSessionStatus.ClosedManually);
        session.UpdatedBy.Should().Be(AdminUserId);
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sesion_inexistente_devuelve_NotFound_no_falla_silenciosamente()
    {
        var (repo, user) = BuildMocks();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserSession?)null);

        var handler = new CloseUserSessionAdminHandler(repo.Object, user.Object);
        var result = await handler.Handle(
            new CloseUserSessionAdminCommand(Guid.NewGuid()),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
    }

    [Fact]
    public async Task Cerrar_una_sesion_ya_cerrada_es_idempotente_segun_el_patron_de_Domain()
    {
        var (repo, user) = BuildMocks();
        var session = UserSession.Create(TenantId, CompanyId, OwnerUserId, BranchId, "device-1");
        session.CloseManually(OwnerUserId);
        repo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var handler = new CloseUserSessionAdminHandler(repo.Object, user.Object);
        var result = await handler.Handle(
            new CloseUserSessionAdminCommand(session.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(UserSessionStatus.ClosedManually);
        session.UpdatedBy.Should().Be(OwnerUserId); // no se reescribe: Close() es idempotente
    }
}

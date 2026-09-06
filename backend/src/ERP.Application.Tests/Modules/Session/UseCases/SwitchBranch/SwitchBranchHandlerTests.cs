using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Session.UseCases.SwitchBranch;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Modules.Session.UseCases.SwitchBranch;

/// <summary>
/// SwitchBranchHandler delega toda la validación (empresa operativa, sucursal existente/activa/
/// de la empresa, y CompanyUserBranch activa) en IBranchAccessGuard — misma fuente única de
/// verdad que BranchScopeBehavior. Estos tests verifican que el handler no reimplementa ninguna
/// regla y que propaga fielmente el resultado del guard. ERP-CORE-CLOSEOUT-05-FIX02 (P1-3) agrega
/// cobertura de la actualización best-effort de UserSession.BranchId tras un switch exitoso.
/// </summary>
public sealed class SwitchBranchHandlerTests
{
    private sealed class Fixture
    {
        public Mock<IBranchAccessGuard> Guard { get; } = new();
        public Mock<IUserSessionRepository> UserSessions { get; } = new();

        public Fixture()
        {
            UserSessions
                .Setup(r =>
                    r.GetActiveSessionsAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .ReturnsAsync(Array.Empty<UserSession>());
        }

        public SwitchBranchHandler BuildHandler() => new(Guard.Object, UserSessions.Object);
    }

    [Fact]
    public async Task Sucursal_valida_y_autorizada_retorna_exito_con_datos_de_la_sucursal()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        branchId,
                        "Matriz",
                        true
                    )
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new SwitchBranchCommand(branchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(branchId);
        result.Value!.Name.Should().Be("Matriz");
        result.Value!.IsMainBranch.Should().BeTrue();
    }

    [Fact]
    public async Task Sucursal_inexistente_rechaza()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Failure("Sucursal no encontrada."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new SwitchBranchCommand(branchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Sucursal no encontrada.");
    }

    [Fact]
    public async Task Sucursal_inactiva_rechaza()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Failure("La sucursal está deshabilitada."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new SwitchBranchCommand(branchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La sucursal está deshabilitada.");
    }

    [Fact]
    public async Task Sucursal_del_tenant_sin_CompanyUserBranch_activa_rechaza()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Failure(
                    "No tiene autorización para operar en esta sucursal."
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new SwitchBranchCommand(branchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene autorización para operar en esta sucursal.");
    }

    [Fact]
    public async Task Usuario_con_CompanyUserBranch_activa_permite_el_cambio()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        branchId,
                        "Sucursal Norte",
                        false
                    )
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new SwitchBranchCommand(branchId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        f.Guard.Verify(
            g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Switch_exitoso_actualiza_BranchId_de_la_UserSession_activa_de_la_empresa()
    {
        var f = new Fixture();
        var newBranchId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var oldBranchId = Guid.NewGuid();

        var activeSession = UserSession.Create(tenantId, companyId, userId, oldBranchId, "terminal-1");
        f.UserSessions
            .Setup(r => r.GetActiveSessionsAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { activeSession });
        f.Guard.Setup(g => g.RequireBranchAsync(newBranchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(userId, tenantId, companyId, newBranchId, "Sucursal Norte", false)
                )
            );

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchBranchCommand(newBranchId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        activeSession.BranchId.Should().Be(newBranchId);
        f.UserSessions.Verify(
            r => r.UpdateAsync(activeSession, It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.UserSessions.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// ERP-CORE-BRANCH-SESSION-PERSISTENCE-01: regresión del bug real encontrado en revisión
    /// manual — usuarios en LoginMode.AskBranch (el caso típico) no tienen UserSession activa
    /// al momento de seleccionar sucursal manualmente (LoginHandler deliberadamente no crea una
    /// cuando no puede resolver la sucursal en el login). Antes, switch-branch no-opeaba en ese
    /// caso, así que la selección nunca quedaba persistida server-side: una pestaña nueva (o
    /// cualquier limpieza de activeBranchStore en la misma pestaña) no tenía forma de
    /// recuperarla y volvía a pedir sucursal indefinidamente. Ahora debe crear la UserSession.
    /// </summary>
    [Fact]
    public async Task Switch_exitoso_sin_UserSession_activa_crea_una_nueva_con_la_sucursal_elegida()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Result<BranchAccessContext>.Success(
                    new BranchAccessContext(userId, tenantId, companyId, branchId, "Matriz", true)
                )
            );

        UserSession? created = null;
        f.UserSessions
            .Setup(r => r.AddAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()))
            .Callback<UserSession, CancellationToken>((s, _) => created = s)
            .Returns(Task.CompletedTask);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchBranchCommand(branchId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.UserSessions.Verify(
            r => r.AddAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        f.UserSessions.Verify(
            r => r.UpdateAsync(It.IsAny<UserSession>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        f.UserSessions.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        created.Should().NotBeNull();
        created!.TenantId.Should().Be(tenantId);
        created.CompanyId.Should().Be(companyId);
        created.IdentityUserId.Should().Be(userId);
        created.BranchId.Should().Be(branchId);
    }
}

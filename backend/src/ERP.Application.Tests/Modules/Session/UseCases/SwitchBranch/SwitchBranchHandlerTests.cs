using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Session.UseCases.SwitchBranch;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Modules.Session.UseCases.SwitchBranch;

/// <summary>
/// SwitchBranchHandler delega toda la validación (empresa operativa, sucursal existente/activa/
/// de la empresa, y CompanyUserBranch activa) en IBranchAccessGuard — misma fuente única de
/// verdad que BranchScopeBehavior. Estos tests verifican que el handler no reimplementa ninguna
/// regla y que propaga fielmente el resultado del guard.
/// </summary>
public sealed class SwitchBranchHandlerTests
{
    private sealed class Fixture
    {
        public Mock<IBranchAccessGuard> Guard { get; } = new();

        public SwitchBranchHandler BuildHandler() => new(Guard.Object);
    }

    [Fact]
    public async Task Sucursal_valida_y_autorizada_retorna_exito_con_datos_de_la_sucursal()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Success(
                new BranchAccessContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), branchId, "Matriz", true)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchBranchCommand(branchId), CancellationToken.None);

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
        var result = await handler.Handle(new SwitchBranchCommand(branchId), CancellationToken.None);

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
        var result = await handler.Handle(new SwitchBranchCommand(branchId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La sucursal está deshabilitada.");
    }

    [Fact]
    public async Task Sucursal_del_tenant_sin_CompanyUserBranch_activa_rechaza()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Failure("No tiene autorización para operar en esta sucursal."));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchBranchCommand(branchId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("No tiene autorización para operar en esta sucursal.");
    }

    [Fact]
    public async Task Usuario_con_CompanyUserBranch_activa_permite_el_cambio()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Success(
                new BranchAccessContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), branchId, "Sucursal Norte", false)));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new SwitchBranchCommand(branchId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        f.Guard.Verify(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()), Times.Once);
    }
}

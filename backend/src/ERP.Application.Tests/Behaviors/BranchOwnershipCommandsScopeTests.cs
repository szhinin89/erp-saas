using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Behaviors;

/// <summary>
/// Fase 3 (Branch Ownership) — puntos 2 y 3 de la validación: los 3 comandos de creación de
/// documentos con BranchId (CreateSalesDraftCommand, CreatePurchaseDraftCommand,
/// OpenCashSessionCommand) deben (a) rechazar sin contexto de sucursal antes de llegar al
/// handler — mismo BranchScopeBehavior/IBranchAccessGuard que el resto del sistema, sin
/// duplicar la regla — y (b) no exponer BranchId como parámetro de entrada: el contrato del
/// comando no tiene forma de que el cliente lo envíe; el valor persistido sale exclusivamente
/// de ICurrentBranch dentro del handler (ver SalesDraftUseCases/PurchaseDraftUseCases/
/// OpenCashSessionUseCases).
/// </summary>
public sealed class BranchOwnershipCommandsScopeTests
{
    private sealed class Fixture
    {
        public Mock<IBranchAccessGuard> Guard { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
    }

    private static RequestHandlerDelegate<Result<string>> NextReturning(Result<string> value, Action? onCalled = null)
        => _ =>
        {
            onCalled?.Invoke();
            return Task.FromResult(value);
        };

    public static IEnumerable<object[]> BranchOwnershipCreationCommands()
    {
        yield return new object[] { new CreateSalesDraftCommand(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.UtcNow), new List<SalesLineInput>()) };
        yield return new object[] { new CreatePurchaseDraftCommand(Guid.NewGuid(), "01", "001-001-000000001", DateOnly.FromDateTime(DateTime.UtcNow), new List<PurchaseLineInput>()) };
        yield return new object[] { new OpenCashSessionCommand(Guid.NewGuid(), 0m) };
    }

    [Theory]
    [MemberData(nameof(BranchOwnershipCreationCommands))]
    public void Los_3_comandos_de_creacion_implementan_IBranchScopedRequest(object command)
    {
        command.Should().BeAssignableTo<IBranchScopedRequest>();
    }

    [Theory]
    [MemberData(nameof(BranchOwnershipCreationCommands))]
    public void Ningun_comando_de_creacion_expone_BranchId_como_parametro(object command)
    {
        command.GetType().GetProperty("BranchId").Should().BeNull(
            "BranchId nunca debe poder llegar desde el cliente — se asigna en el handler desde ICurrentBranch");
    }

    [Theory]
    [MemberData(nameof(BranchOwnershipCreationCommands))]
    public async Task Sin_contexto_de_sucursal_rechaza_antes_de_llegar_al_handler(object command)
    {
        var f = new Fixture();
        f.Branch.Setup(b => b.HasBranchContext).Returns(false);

        var behavior = BuildBehaviorFor(f, command);
        var nextCalled = false;

        var act = async () => await InvokeAsync(
            behavior, command, NextReturning(Result<string>.Success("no-debe-llegar"), () => nextCalled = true));

        await act.Should().ThrowAsync<BranchScopeException>();
        nextCalled.Should().BeFalse("el handler de creación nunca debe ejecutarse sin sucursal activa");
        f.Guard.Verify(g => g.RequireBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [MemberData(nameof(BranchOwnershipCreationCommands))]
    public async Task Con_sucursal_valida_y_autorizada_permite_llegar_al_handler(object command)
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Branch.Setup(b => b.HasBranchContext).Returns(true);
        f.Branch.Setup(b => b.BranchId).Returns(branchId);
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Success(
                new BranchAccessContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), branchId, "Matriz", true)));

        var behavior = BuildBehaviorFor(f, command);
        var expected = Result<string>.Success("ok");
        var nextCalled = false;

        var result = await InvokeAsync(behavior, command, NextReturning(expected, () => nextCalled = true));

        result.Should().Be(expected);
        nextCalled.Should().BeTrue();
    }

    private static object BuildBehaviorFor(Fixture f, object request)
    {
        var requestType = request.GetType();
        var behaviorType = typeof(BranchScopeBehavior<,>).MakeGenericType(requestType, typeof(Result<string>));
        return Activator.CreateInstance(behaviorType, f.Guard.Object, f.Branch.Object)!;
    }

    private static async Task<Result<string>> InvokeAsync(
        object behavior, object request, RequestHandlerDelegate<Result<string>> next)
    {
        var method = behavior.GetType().GetMethod("Handle")!;
        var task = (Task<Result<string>>)method.Invoke(behavior, [request, next, CancellationToken.None])!;
        return await task;
    }
}

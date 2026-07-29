using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Sales.UseCases;
using ERP.Application.Modules.Sales.UseCases.GetSalesInvoiceDefaults;
using ERP.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Behaviors;

/// <summary>
/// Fase I-6B — cierre de gaps de IBranchScopedRequest en el bloque Sales
/// (GetSalesInvoiceListQuery, GetSalesInvoiceDefaultsQuery, GetReceivableByInvoiceQuery,
/// GetReceivablesListQuery). No reimplementa la regla de BranchScopeBehavior (ver
/// BranchScopeBehaviorTests para la cobertura genérica) — solo confirma que estas cuatro
/// requests concretas quedaron correctamente marcadas y por tanto pasan por el mismo
/// enforcement único (IBranchAccessGuard) que el resto de Sales.
/// </summary>
public sealed class SalesBranchScopeTests
{
    private sealed class Fixture
    {
        public Mock<IBranchAccessGuard> Guard { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();
    }

    private static RequestHandlerDelegate<Result<string>> NextReturning(
        Result<string> value,
        Action? onCalled = null
    ) =>
        _ =>
        {
            onCalled?.Invoke();
            return Task.FromResult(value);
        };

    public static IEnumerable<object[]> BranchScopedSalesRequests()
    {
        yield return new object[] { new GetSalesInvoiceListQuery() };
        yield return new object[] { new GetSalesInvoiceDefaultsQuery() };
        yield return new object[] { new GetReceivableByInvoiceQuery(Guid.NewGuid()) };
        yield return new object[] { new GetReceivablesListQuery() };
    }

    [Theory]
    [MemberData(nameof(BranchScopedSalesRequests))]
    public void Todas_las_requests_del_bloque_Sales_implementan_IBranchScopedRequest(object request)
    {
        request.Should().BeAssignableTo<IBranchScopedRequest>();
    }

    [Theory]
    [MemberData(nameof(BranchScopedSalesRequests))]
    public async Task Sin_contexto_de_sucursal_rechaza(object request)
    {
        var f = new Fixture();
        f.Branch.Setup(b => b.HasBranchContext).Returns(false);

        var behavior = BuildBehaviorFor(f, request);
        var nextCalled = false;

        var act = async () =>
            await InvokeAsync(
                behavior,
                request,
                NextReturning(Result<string>.Success("no-debe-llegar"), () => nextCalled = true)
            );

        await act.Should().ThrowAsync<BranchScopeException>();
        nextCalled.Should().BeFalse();
        f.Guard.Verify(
            g => g.RequireBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Theory]
    [MemberData(nameof(BranchScopedSalesRequests))]
    public async Task Con_sucursal_valida_y_autorizada_permite_ejecucion(object request)
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Branch.Setup(b => b.HasBranchContext).Returns(true);
        f.Branch.Setup(b => b.BranchId).Returns(branchId);
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

        var behavior = BuildBehaviorFor(f, request);
        var expected = Result<string>.Success("ok");
        var nextCalled = false;

        var result = await InvokeAsync(
            behavior,
            request,
            NextReturning(expected, () => nextCalled = true)
        );

        result.Should().Be(expected);
        nextCalled.Should().BeTrue();
        f.Guard.Verify(
            g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // MediatR pipeline behaviors son genéricos por tipo concreto de request — se resuelve
    // reflexivamente para poder parametrizar el test sobre los 4 tipos de Sales sin 4 clases
    // de test casi idénticas.
    private static object BuildBehaviorFor(Fixture f, object request)
    {
        var requestType = request.GetType();
        var behaviorType = typeof(BranchScopeBehavior<,>).MakeGenericType(
            requestType,
            typeof(Result<string>)
        );
        return Activator.CreateInstance(behaviorType, f.Guard.Object, f.Branch.Object)!;
    }

    private static async Task<Result<string>> InvokeAsync(
        object behavior,
        object request,
        RequestHandlerDelegate<Result<string>> next
    )
    {
        var method = behavior.GetType().GetMethod("Handle")!;
        var task =
            (Task<Result<string>>)method.Invoke(behavior, [request, next, CancellationToken.None])!;
        return await task;
    }
}

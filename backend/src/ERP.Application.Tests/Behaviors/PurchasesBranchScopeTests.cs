using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Behaviors;

/// <summary>
/// Cierre de gaps de IBranchScopedRequest en Purchases (auditoría BranchScopeBehavior
/// completa): CalculateRetentionQuery (operaba puntualmente sobre PurchaseInvoice, igual que
/// ConfirmPurchase/CancelPurchase, que ya son branch-scoped) y GetPurchaseListQuery (mismo
/// criterio que GetSalesInvoiceListQuery: exigencia de contexto, sin filtrar resultados por
/// sucursal). No reimplementa la regla de BranchScopeBehavior — ver BranchScopeBehaviorTests
/// para la cobertura genérica; esto solo confirma que ambas requests quedaron correctamente
/// marcadas.
/// </summary>
public sealed class PurchasesBranchScopeTests
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

    public static IEnumerable<object[]> BranchScopedPurchasesRequests()
    {
        yield return new object[] { new CalculateRetentionQuery(Guid.NewGuid()) };
        yield return new object[] { new GetPurchaseListQuery() };
    }

    [Theory]
    [MemberData(nameof(BranchScopedPurchasesRequests))]
    public void Ambas_requests_implementan_IBranchScopedRequest(object request)
    {
        request.Should().BeAssignableTo<IBranchScopedRequest>();
    }

    [Theory]
    [MemberData(nameof(BranchScopedPurchasesRequests))]
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
    [MemberData(nameof(BranchScopedPurchasesRequests))]
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

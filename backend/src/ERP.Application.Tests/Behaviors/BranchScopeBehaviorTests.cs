using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Branches;
using ERP.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Behaviors;

/// <summary>
/// Fase I-3 — prueba el comportamiento genérico de BranchScopeBehavior: orquesta (obtiene
/// ICurrentBranch, verifica contexto, invoca exclusivamente IBranchAccessGuard) sin contener
/// ninguna regla de negocio propia. Mismo criterio de test que CompanyScopeBehaviorTests.
/// </summary>
public sealed class BranchScopeBehaviorTests
{
    private sealed record FakeBranchScopedRequest : IRequest<Result<string>>, IBranchScopedRequest;

    private sealed record FakeCompanyOnlyRequest : IRequest<Result<string>>, ICompanyScopedRequest;

    private sealed class Fixture
    {
        public Mock<IBranchAccessGuard> Guard { get; } = new();
        public Mock<ICurrentBranch> Branch { get; } = new();

        public BranchScopeBehavior<TRequest, Result<string>> BuildBehavior<TRequest>() where TRequest : notnull
            => new(Guard.Object, Branch.Object);
    }

    private static RequestHandlerDelegate<Result<string>> NextReturning(Result<string> value, Action? onCalled = null)
        => _ =>
        {
            onCalled?.Invoke();
            return Task.FromResult(value);
        };

    [Fact]
    public async Task Request_branch_scoped_con_contexto_valido_invoca_next()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Branch.Setup(b => b.HasBranchContext).Returns(true);
        f.Branch.Setup(b => b.BranchId).Returns(branchId);
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Success(
                new BranchAccessContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), branchId, "Matriz", false)));

        var behavior = f.BuildBehavior<FakeBranchScopedRequest>();
        var expected = Result<string>.Success("ok");
        var nextCalled = false;

        var result = await behavior.Handle(
            new FakeBranchScopedRequest(), NextReturning(expected, () => nextCalled = true), CancellationToken.None);

        result.Should().Be(expected);
        nextCalled.Should().BeTrue();
        f.Guard.Verify(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Request_branch_scoped_sin_contexto_de_sucursal_nunca_llega_al_handler()
    {
        var f = new Fixture();
        f.Branch.Setup(b => b.HasBranchContext).Returns(false);

        var behavior = f.BuildBehavior<FakeBranchScopedRequest>();
        var nextCalled = false;

        var act = async () => await behavior.Handle(
            new FakeBranchScopedRequest(), NextReturning(Result<string>.Success("no-debe-llegar"), () => nextCalled = true), CancellationToken.None);

        await act.Should().ThrowAsync<BranchScopeException>();
        nextCalled.Should().BeFalse("el handler nunca debe ejecutarse sin contexto de sucursal (header X-Branch-Id ausente)");
        f.Guard.Verify(g => g.RequireBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sucursal_no_autorizada_via_CompanyUserBranch_nunca_llega_al_handler()
    {
        var f = new Fixture();
        var branchId = Guid.NewGuid();
        f.Branch.Setup(b => b.HasBranchContext).Returns(true);
        f.Branch.Setup(b => b.BranchId).Returns(branchId);
        f.Guard.Setup(g => g.RequireBranchAsync(branchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<BranchAccessContext>.Failure("No tiene autorización para operar en esta sucursal."));

        var behavior = f.BuildBehavior<FakeBranchScopedRequest>();
        var nextCalled = false;

        var act = async () => await behavior.Handle(
            new FakeBranchScopedRequest(), NextReturning(Result<string>.Success("no-debe-llegar"), () => nextCalled = true), CancellationToken.None);

        await act.Should().ThrowAsync<BranchScopeException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Request_que_solo_implementa_ICompanyScopedRequest_no_pasa_por_BranchScopeBehavior()
    {
        var f = new Fixture();
        var behavior = f.BuildBehavior<FakeCompanyOnlyRequest>();
        var expected = Result<string>.Success("ok");
        var nextCalled = false;

        var result = await behavior.Handle(
            new FakeCompanyOnlyRequest(), NextReturning(expected, () => nextCalled = true), CancellationToken.None);

        result.Should().Be(expected);
        nextCalled.Should().BeTrue();
        f.Guard.Verify(g => g.RequireBranchAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "una request que no es IBranchScopedRequest jamás debe disparar IBranchAccessGuard");
    }
}

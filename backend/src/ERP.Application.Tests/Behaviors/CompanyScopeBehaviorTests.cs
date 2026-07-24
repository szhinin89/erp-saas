using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Companies;
using ERP.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Behaviors;

/// <summary>
/// Fase S1 (hardening 5C): prueba directamente el mecanismo que ahora protege
/// GetCompanyUserMembershipsAdminQuery/GetCompanyUserPreferencesAdminQuery/
/// UpdateCompanyUserPreferencesAdminCommand/GetCompanyUserBranchesAdminQuery/
/// UpdateCompanyUserBranchesAdminCommand — todas ahora implementan IRequiresCompanyContext
/// (mismo marker que UpsertCompanyUserMembershipAdminCommand/RevokeCompanyUserMembershipAdminCommand,
/// Fase I-A), lo que fuerza a CompanyScopeBehavior a llamar ICompanyAccessGuard.RequireCurrentCompanyAsync
/// antes de que el handler se ejecute. Antes de este fix, esas cinco requests no llevaban ningún
/// marker y su única defensa era un chequeo manual contra ICurrentCompany.CompanyId — un valor
/// leído del header X-Company-Id (no de un claim firmado), sin revalidar tenant ni membership real,
/// y sin que el bypass de rol Admin (RuntimePermissionAuthorizer) pasara nunca por este guard.
/// Este test no vuelve a probar cada handler individualmente (eso ya lo cubren sus propios tests) —
/// prueba el comportamiento genérico del behavior, que es lo que ahora comparten los cinco.
/// </summary>
public sealed class CompanyScopeBehaviorTests
{
    private sealed record FakeCompanyScopedRequest : IRequest<Result<string>>, IRequiresCompanyContext;

    private sealed class Fixture
    {
        public Mock<ICompanyAccessGuard> Guard { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();

        public Fixture()
        {
            // Tenant activo por defecto en todos los escenarios — lo que se prueba aquí es
            // específicamente la validación de empresa (RequireCurrentCompanyAsync), no la de tenant.
            Guard.Setup(g => g.RequireActiveTenantAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<Guid>.Success(Guid.NewGuid()));
        }

        public CompanyScopeBehavior<FakeCompanyScopedRequest, Result<string>> BuildBehavior()
            => new(Guard.Object, Company.Object);
    }

    private static RequestHandlerDelegate<Result<string>> NextReturning(Result<string> value, Action? onCalled = null)
        => _ =>
        {
            onCalled?.Invoke();
            return Task.FromResult(value);
        };

    [Fact]
    public async Task Acceso_correcto_invoca_next_cuando_la_empresa_activa_tiene_membership_real()
    {
        var f = new Fixture();
        f.Guard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Success(
                new CompanyAccessContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Admin", true, true)));

        var behavior = f.BuildBehavior();
        var expected = Result<string>.Success("ok");
        var nextCalled = false;

        var result = await behavior.Handle(
            new FakeCompanyScopedRequest(), NextReturning(expected, () => nextCalled = true), CancellationToken.None);

        result.Should().Be(expected);
        nextCalled.Should().BeTrue();
        f.Guard.Verify(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Header_X_Company_Id_manipulado_hacia_una_empresa_sin_membership_no_permite_acceso()
    {
        var f = new Fixture();
        f.Guard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Failure("No tiene acceso a esta empresa."));

        var behavior = f.BuildBehavior();
        var nextCalled = false;

        var act = async () => await behavior.Handle(
            new FakeCompanyScopedRequest(), NextReturning(Result<string>.Success("no-debe-llegar"), () => nextCalled = true), CancellationToken.None);

        await act.Should().ThrowAsync<CompanyScopeException>();
        nextCalled.Should().BeFalse("el handler nunca debe ejecutarse si la empresa del header no tiene membership real");
    }

    [Fact]
    public async Task Empresa_de_otro_tenant_no_permite_acceso()
    {
        var f = new Fixture();
        // Mismo mensaje que CompanyAccessGuard.RequireMembershipAsync devuelve cuando
        // company.TenantId != tenantId del JWT — la empresa existe, pero no es del tenant del caller.
        f.Guard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Failure("Empresa no encontrada o no pertenece al tenant activo."));

        var behavior = f.BuildBehavior();
        var nextCalled = false;

        var act = async () => await behavior.Handle(
            new FakeCompanyScopedRequest(), NextReturning(Result<string>.Success("no-debe-llegar"), () => nextCalled = true), CancellationToken.None);

        await act.Should().ThrowAsync<CompanyScopeException>();
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_contexto_de_empresa_activa_no_permite_acceso()
    {
        var f = new Fixture();
        f.Guard.Setup(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CompanyAccessContext>.Failure("No hay empresa operativa seleccionada."));

        var behavior = f.BuildBehavior();
        var act = async () => await behavior.Handle(
            new FakeCompanyScopedRequest(), NextReturning(Result<string>.Success("no-debe-llegar")), CancellationToken.None);

        await act.Should().ThrowAsync<CompanyScopeException>();
    }

    [Fact]
    public async Task Tenant_inactivo_no_permite_acceso_incluso_antes_de_evaluar_la_empresa()
    {
        var f = new Fixture();
        f.Guard.Setup(g => g.RequireActiveTenantAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<Guid>.Failure("Tenant no válido o inactivo."));

        var behavior = f.BuildBehavior();
        var act = async () => await behavior.Handle(
            new FakeCompanyScopedRequest(), NextReturning(Result<string>.Success("no-debe-llegar")), CancellationToken.None);

        await act.Should().ThrowAsync<CompanyScopeException>();
        f.Guard.Verify(g => g.RequireCurrentCompanyAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

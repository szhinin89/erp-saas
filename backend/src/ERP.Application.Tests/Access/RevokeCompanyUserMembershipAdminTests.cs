using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembershipAdmin;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Interfaces;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase I-A: <see cref="RevokeCompanyUserMembershipAdminHandler"/> es un envoltorio delgado sobre
/// <see cref="RevokeCompanyUserMembershipHandler"/> (Fase D, ya probado) — estos tests verifican
/// únicamente el agregado propio de esta fase (resolución de contexto + aislamiento de empresa).
/// La lógica de revocación en sí (idempotencia sobre membership inexistente/ya inactiva, etc.) no
/// se reimplementa ni se vuelve a probar aquí.
/// </summary>
public sealed class RevokeCompanyUserMembershipAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Username = "ana.perez";

    private static Tenant NewTenant() =>
        Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: CreatedBy);

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public CurrentTenantStub(Guid tenantId) => TenantId = tenantId;

        public Guid TenantId { get; }
        public string? Slug => null;
    }

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public CurrentCompanyStub(Guid companyId) => CompanyId = companyId;

        public Guid CompanyId { get; }
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class Fixture
    {
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyRepository> CompanyRepo { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public RevokeCompanyUserMembershipAdminHandler BuildHandler(
            Guid tenantId,
            Guid companyId
        ) =>
            new(
                new CurrentTenantStub(tenantId),
                new CurrentCompanyStub(companyId),
                TenantRepo.Object,
                CompanyRepo.Object,
                Mediator.Object
            );
    }

    [Fact]
    public async Task Revoca_la_membresia_de_la_empresa_activa_delegando_en_el_UseCase_de_Fase_D()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);

        RevokeCompanyUserMembershipCommand? sentCommand = null;
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<RevokeCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<IRequest<Result<object>>, CancellationToken>(
                (cmd, _) => sentCommand = (RevokeCompanyUserMembershipCommand)cmd
            )
            .ReturnsAsync(Result<object>.Success(new { }));

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new RevokeCompanyUserMembershipAdminCommand(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        sentCommand.Should().NotBeNull();
        sentCommand!.TenantId.Should().Be(tenant.Id);
        sentCommand.CompanyId.Should().Be(company.Id);
        sentCommand.Username.Should().Be(Username);
    }

    [Fact]
    public async Task Empresa_activa_inexistente_en_el_tenant_devuelve_NotFound_y_no_delega_en_Fase_D()
    {
        var tenant = NewTenant();
        var otherActiveCompanyId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(
                    otherActiveCompanyId,
                    tenant.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Company?)null);

        var handler = f.BuildHandler(tenant.Id, otherActiveCompanyId);
        var result = await handler.Handle(
            new RevokeCompanyUserMembershipAdminCommand(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<RevokeCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    /// <summary>
    /// El email no corresponde a ningún IdentityUser — RevokeCompanyUserMembershipHandler (Fase D)
    /// devuelve Failure("Usuario no existe.") en ese caso (a diferencia de una membresía inexistente
    /// o ya inactiva para un usuario real, que ese mismo handler trata como no-op idempotente por
    /// diseño — comportamiento heredado, no modificado en esta fase).
    /// </summary>
    [Fact]
    public async Task Usuario_inexistente_propaga_el_Failure_del_UseCase_de_Fase_D()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);
        f.CompanyRepo.Setup(r =>
                r.GetByIdForTenantAsync(company.Id, tenant.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(company);
        f.Mediator.Setup(m =>
                m.Send(
                    It.IsAny<RevokeCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Result<object>.Failure("Usuario no existe."));

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new RevokeCompanyUserMembershipAdminCommand("no-existe"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Usuario no existe.");
    }

    [Fact]
    public async Task Tenant_inexistente_devuelve_NotFound_y_no_delega_en_Fase_D()
    {
        var tenantId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var handler = f.BuildHandler(tenantId, Guid.NewGuid());
        var result = await handler.Handle(
            new RevokeCompanyUserMembershipAdminCommand(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Mediator.Verify(
            m =>
                m.Send(
                    It.IsAny<RevokeCompanyUserMembershipCommand>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }
}

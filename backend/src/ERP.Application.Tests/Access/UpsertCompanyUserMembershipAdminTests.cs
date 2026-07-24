using ERP.Application.Access.UseCases.UpsertCompanyUserMembership;
using ERP.Application.Access.UseCases.UpsertCompanyUserMembershipAdmin;
using ERP.Application.Common;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase I-A: <see cref="UpsertCompanyUserMembershipAdminHandler"/> es un envoltorio delgado sobre
/// <see cref="UpsertCompanyUserMembershipHandler"/> (Fase D, ya probado en
/// UpsertCompanyUserMembershipHandlerTests) — estos tests verifican únicamente el agregado propio
/// de esta fase: resolución de TenantId/CompanyId desde el contexto autenticado (nunca del
/// request) y el aislamiento cuando la empresa activa no coincide con la empresa por defecto del
/// tenant. Ninguna regla de membership/branch/preferences se reimplementa ni se vuelve a probar
/// aquí.
/// </summary>
public sealed class UpsertCompanyUserMembershipAdminHandlerTests
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
        public Mock<ICompanyProvisioningService> CompanyProvisioning { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();

        public UpsertCompanyUserMembershipAdminHandler BuildHandler(Guid tenantId, Guid companyId) => new(
            new CurrentTenantStub(tenantId),
            new CurrentCompanyStub(companyId),
            TenantRepo.Object,
            CompanyProvisioning.Object,
            Mediator.Object);
    }

    [Fact]
    public async Task Empresa_activa_coincide_con_la_del_tenant_delega_en_el_UseCase_de_Fase_D_y_devuelve_su_resultado()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        f.CompanyProvisioning.Setup(s => s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        UpsertCompanyUserMembershipCommand? sentCommand = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<UpsertCompanyUserMembershipCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<object>>, CancellationToken>((cmd, _) => sentCommand = (UpsertCompanyUserMembershipCommand)cmd)
            .ReturnsAsync(Result<object>.Success(new { }));

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipAdminCommand(Username, "User"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentCommand.Should().NotBeNull();
        sentCommand!.TenantId.Should().Be(tenant.Id);
        sentCommand.Username.Should().Be(Username);
        sentCommand.Role.Should().Be("User");
    }

    [Fact]
    public async Task Actualiza_membresia_existente_reenviando_Role_y_ProfileId_al_UseCase_de_Fase_D()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var profileId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        f.CompanyProvisioning.Setup(s => s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        UpsertCompanyUserMembershipCommand? sentCommand = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<UpsertCompanyUserMembershipCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<object>>, CancellationToken>((cmd, _) => sentCommand = (UpsertCompanyUserMembershipCommand)cmd)
            .ReturnsAsync(Result<object>.Success(new { }));

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipAdminCommand(Username, "Admin", ProfileId: profileId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentCommand!.Role.Should().Be("Admin");
        sentCommand.ProfileId.Should().Be(profileId);
    }

    [Fact]
    public async Task Empresa_activa_distinta_de_la_del_tenant_devuelve_Forbidden_y_no_delega_en_Fase_D()
    {
        var tenant = NewTenant();
        var tenantDefaultCompany = NewCompany(tenant.Id);
        var otherActiveCompanyId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        f.CompanyProvisioning.Setup(s => s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(tenantDefaultCompany);

        var handler = f.BuildHandler(tenant.Id, otherActiveCompanyId);
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipAdminCommand(Username, "User"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        f.Mediator.Verify(m => m.Send(It.IsAny<UpsertCompanyUserMembershipCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Usuario_inexistente_propaga_el_Failure_del_UseCase_de_Fase_D()
    {
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        f.CompanyProvisioning.Setup(s => s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        f.Mediator.Setup(m => m.Send(It.IsAny<UpsertCompanyUserMembershipCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<object>.Failure("Usuario no existe."));

        var handler = f.BuildHandler(tenant.Id, company.Id);
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipAdminCommand("no-existe", "User"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Usuario no existe.");
    }

    [Fact]
    public async Task Tenant_inexistente_devuelve_NotFound_y_no_delega_en_Fase_D()
    {
        var tenantId = Guid.NewGuid();
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenantId, It.IsAny<CancellationToken>())).ReturnsAsync((Tenant?)null);

        var handler = f.BuildHandler(tenantId, Guid.NewGuid());
        var result = await handler.Handle(
            new UpsertCompanyUserMembershipAdminCommand(Username, "User"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.Mediator.Verify(m => m.Send(It.IsAny<UpsertCompanyUserMembershipCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

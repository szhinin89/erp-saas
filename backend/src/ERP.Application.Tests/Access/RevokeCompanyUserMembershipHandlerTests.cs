using ERP.Application.Access.Caching;
using ERP.Application.Access.UseCases.RevokeCompanyUserMembership;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel.Security;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Tenants.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Sin cobertura previa a este archivo (solo existía RevokeCompanyUserMembershipAdminTests, que
/// mockea IMediator y no ejercita este handler). Cubre el comportamiento heredado (revocar
/// usuario no-admin) y el nuevo CompanyAdministratorInvariant (no se puede dejar la empresa sin
/// ningún Administrador activo).
/// </summary>
public sealed class RevokeCompanyUserMembershipHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Username = "ana.perez";

    private static IdentityUser NewUser() =>
        IdentityUser.Create(Username, "Ana", "Perez", "ana@test.com", "hash", CreatedBy);

    private static Tenant NewTenant() =>
        Tenant.Create("Test Tenant", $"test-{Guid.NewGuid():N}"[..16], CreatedBy);

    private static Company NewCompany(Guid tenantId) =>
        Company.CreateManaged(tenantId, "1790012345001", "Test S.A.", createdBy: CreatedBy);

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<ICurrentUser> CurrentUser { get; } = new();
        public Mock<ITenantRepository> TenantRepo { get; } = new();
        public Mock<ICompanyProvisioningService> CompanyProvisioning { get; } = new();
        public Mock<IPermissionsCacheInvalidator> PermissionsCache { get; } = new();
        public Mock<INavigationBuilder> NavigationBuilder { get; } = new();

        public Fixture()
        {
            CurrentUser.SetupGet(x => x.UserId).Returns(CreatedBy);
        }

        public RevokeCompanyUserMembershipHandler BuildHandler() => new(
            AccessRepo.Object, CurrentUser.Object, TenantRepo.Object, CompanyProvisioning.Object,
            PermissionsCache.Object, NavigationBuilder.Object);
    }

    private static Fixture BuildBaseFixture(
        IdentityUser user, Tenant tenant, Company company, CompanyUserMembership membership,
        IReadOnlyList<CompanyUserMembership> activeMemberships)
    {
        var f = new Fixture();
        f.TenantRepo.Setup(r => r.GetByIdAsync(tenant.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tenant);
        f.CompanyProvisioning.Setup(s => s.EnsureDefaultCompanyAsync(tenant, It.IsAny<CancellationToken>())).ReturnsAsync(company);
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(company.Id, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipsByCompanyAsync(company.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMemberships);
        return f;
    }

    [Fact]
    public async Task Revocar_al_unico_Admin_activo_falla_con_el_mensaje_del_invariante_y_no_persiste_nada()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, SecurityRoles.Admin, null, CreatedBy);
        var f = BuildBaseFixture(user, tenant, company, membership, new[] { membership });

        var handler = f.BuildHandler();
        var result = await handler.Handle(new RevokeCompanyUserMembershipCommand(tenant.Id, Username), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Be("La empresa debe conservar al menos un administrador activo.");
        membership.IsActive.Should().BeTrue();
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.PermissionsCache.Verify(c => c.InvalidateUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Revocar_Admin_cuando_existe_otro_Admin_activo_succeeds()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, SecurityRoles.Admin, null, CreatedBy);
        var otherAdmin = CompanyUserMembership.Create(company.Id, Guid.NewGuid(), SecurityRoles.Admin, null, CreatedBy);
        var f = BuildBaseFixture(user, tenant, company, membership, new[] { membership, otherAdmin });

        var handler = f.BuildHandler();
        var result = await handler.Handle(new RevokeCompanyUserMembershipCommand(tenant.Id, Username), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        membership.IsActive.Should().BeFalse();
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Revocar_usuario_no_admin_succeeds_sin_consultar_el_invariante_por_otros_admins()
    {
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, "User", null, CreatedBy);
        var admin = CompanyUserMembership.Create(company.Id, Guid.NewGuid(), SecurityRoles.Admin, null, CreatedBy);
        var f = BuildBaseFixture(user, tenant, company, membership, new[] { membership, admin });

        var handler = f.BuildHandler();
        var result = await handler.Handle(new RevokeCompanyUserMembershipCommand(tenant.Id, Username), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        membership.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Auto_revocacion_del_unico_Admin_falla_igual_que_si_lo_revocara_otro_admin()
    {
        // El invariante nunca pregunta quién es el actor — mismo resultado sea auto-revocación o no.
        var user = NewUser();
        var tenant = NewTenant();
        var company = NewCompany(tenant.Id);
        var membership = CompanyUserMembership.Create(company.Id, user.Id, SecurityRoles.Admin, null, CreatedBy);
        var f = BuildBaseFixture(user, tenant, company, membership, new[] { membership });
        f.CurrentUser.SetupGet(x => x.UserId).Returns(user.Id); // el propio Admin revocado es quien ejecuta la acción

        var handler = f.BuildHandler();
        var result = await handler.Handle(new RevokeCompanyUserMembershipCommand(tenant.Id, Username), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La empresa debe conservar al menos un administrador activo.");
    }
}

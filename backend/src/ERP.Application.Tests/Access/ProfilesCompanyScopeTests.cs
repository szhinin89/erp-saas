using ERP.Application.Access.Caching;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Access.UseCases.Profiles;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// SEC-05 — los perfiles pertenecen a <c>TenantId + CompanyId</c>. Cada handler de perfiles
/// resuelve el perfil con el tenant y la empresa activa (ICurrentCompany, validada por
/// CompanyScopeBehavior): un ProfileId de otra empresa (o de otro tenant) nunca se resuelve, y el
/// handler falla sin leer, mutar ni invalidar cachés. Crear y listar operan sobre la empresa activa.
/// </summary>
public sealed class ProfilesCompanyScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyAId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class CurrentCompanyStub(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => companyId != Guid.Empty;
    }

    private static Mock<ICurrentTenant> Tenant()
    {
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);
        return tenant;
    }

    private static Mock<ICurrentUser> User()
    {
        var user = new Mock<ICurrentUser>();
        user.Setup(u => u.UserId).Returns(UserId);
        return user;
    }

    [Fact]
    public async Task CreateProfile_asigna_tenant_y_empresa_activa()
    {
        var repo = new Mock<IAccessRepository>();
        AccessProfile? added = null;
        repo.Setup(r => r.AddProfileAsync(It.IsAny<AccessProfile>(), It.IsAny<CancellationToken>()))
            .Callback<AccessProfile, CancellationToken>((p, _) => added = p)
            .Returns(Task.CompletedTask);

        var handler = new CreateProfileHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId),
            User().Object
        );

        var result = await handler.Handle(
            new CreateProfileCommand("Cajero", null),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.TenantId.Should().Be(TenantId);
        added.CompanyId.Should().Be(CompanyAId);
    }

    [Fact]
    public void Empresas_distintas_pueden_crear_perfiles_con_el_mismo_nombre_sin_compartir_identidad()
    {
        var companyBId = Guid.NewGuid();
        var profileA = AccessProfile.Create(TenantId, CompanyAId, "Cajero", null, UserId);
        var profileB = AccessProfile.Create(TenantId, companyBId, "Cajero", null, UserId);

        profileA.Id.Should().NotBe(profileB.Id);
        profileA.CompanyId.Should().Be(CompanyAId);
        profileB.CompanyId.Should().Be(companyBId);
    }

    [Fact]
    public void Create_sin_empresa_falla()
    {
        var act = () => AccessProfile.Create(TenantId, Guid.Empty, "Cajero", null, UserId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task GetProfiles_lista_solo_la_empresa_activa()
    {
        var repo = new Mock<IAccessRepository>();
        var own = AccessProfile.Create(TenantId, CompanyAId, "Ventas", null, UserId);
        repo.Setup(r =>
                r.GetProfilesByCompanyAsync(
                    TenantId,
                    CompanyAId,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync([own]);

        var handler = new GetProfilesHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId)
        );

        var result = await handler.Handle(new GetProfilesQuery(true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(p => p.Id).Should().BeEquivalentTo([own.Id]);
        repo.Verify(
            r =>
                r.GetProfilesByCompanyAsync(
                    It.IsAny<Guid>(),
                    It.Is<Guid>(c => c != CompanyAId),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task UpdateProfile_perfil_de_otra_empresa_falla_sin_actualizar_ni_invalidar_cache()
    {
        var repo = new Mock<IAccessRepository>();
        var permissionsCache = PermissionsCacheInvalidatorMocks.Healthy();
        var navigationBuilder = new Mock<INavigationBuilder>();

        var profileOfCompanyB = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(
                    TenantId,
                    CompanyAId,
                    profileOfCompanyB,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = new UpdateProfileHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId),
            User().Object,
            permissionsCache.Object,
            navigationBuilder.Object
        );

        var result = await handler.Handle(
            new UpdateProfileCommand(profileOfCompanyB, "Nuevo nombre", null, false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Perfil no encontrado");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        permissionsCache.Verify(
            c => c.TryCompleteAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        navigationBuilder.Verify(
            n => n.InvalidateCache(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UpdateProfile_desactivar_invalida_solo_las_memberships_de_la_empresa_activa()
    {
        var repo = new Mock<IAccessRepository>();
        var permissionsCache = PermissionsCacheInvalidatorMocks.Healthy();
        var navigationBuilder = new Mock<INavigationBuilder>();

        var profile = AccessProfile.Create(TenantId, CompanyAId, "Ventas", null, UserId);
        var memberUserId = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(
                    TenantId,
                    CompanyAId,
                    profile.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(profile);
        repo.Setup(r =>
                r.GetCompanyUserMembershipsByCompanyAsync(
                    CompanyAId,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                [CompanyUserMembership.Create(CompanyAId, memberUserId, "User", profile.Id, UserId)]
            );

        var handler = new UpdateProfileHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId),
            User().Object,
            permissionsCache.Object,
            navigationBuilder.Object
        );

        var result = await handler.Handle(
            new UpdateProfileCommand(profile.Id, "Ventas", null, IsActive: false),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        profile.IsActive.Should().BeFalse();
        permissionsCache.Verify(
            c => c.TryCompleteAsync(PermissionsCacheScope.Company(CompanyAId), It.IsAny<CancellationToken>()),
            Times.Once
        );
        permissionsCache.Verify(
            c =>
                c.TryCompleteAsync(
                    It.Is<PermissionsCacheScope>(s => s != PermissionsCacheScope.Company(CompanyAId)),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        navigationBuilder.Verify(n => n.InvalidateCache(TenantId, CompanyAId, memberUserId));
        repo.Verify(
            r =>
                r.GetCompanyUserMembershipsByTenantAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetProfilePermissions_perfil_de_otra_empresa_falla_sin_leer_permisos()
    {
        var repo = new Mock<IAccessRepository>();
        var profileOfCompanyB = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(
                    TenantId,
                    CompanyAId,
                    profileOfCompanyB,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = new GetProfilePermissionsHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId)
        );

        var result = await handler.Handle(
            new GetProfilePermissionsQuery(profileOfCompanyB),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Perfil no existe");
        repo.Verify(
            r =>
                r.GetProfilePermissionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetProfilePermissionAudit_perfil_de_otra_empresa_devuelve_NotFound_sin_leer_permisos()
    {
        var repo = new Mock<IAccessRepository>();
        var profileOfCompanyB = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(
                    TenantId,
                    CompanyAId,
                    profileOfCompanyB,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = new GetProfilePermissionAuditHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId)
        );

        var result = await handler.Handle(
            new GetProfilePermissionAuditQuery(profileOfCompanyB),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        repo.Verify(
            r =>
                r.GetProfilePermissionsAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task GetProfilePermissions_perfil_de_la_empresa_activa_se_resuelve_correctamente()
    {
        var repo = new Mock<IAccessRepository>();
        var profile = AccessProfile.Create(TenantId, CompanyAId, "Test", null, UserId);
        repo.Setup(r =>
                r.GetProfileByIdAsync(
                    TenantId,
                    CompanyAId,
                    profile.Id,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(profile);
        repo.Setup(r =>
                r.GetProfilePermissionsAsync(TenantId, profile.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<AccessProfilePermission>());

        var handler = new GetProfilePermissionsHandler(
            repo.Object,
            Tenant().Object,
            new CurrentCompanyStub(CompanyAId)
        );

        var result = await handler.Handle(
            new GetProfilePermissionsQuery(profile.Id),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProfileId.Should().Be(profile.Id);
    }
}

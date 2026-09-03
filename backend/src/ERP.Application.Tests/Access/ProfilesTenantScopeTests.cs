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
/// MEDIO cluster (auditoría multi-tenant) — UpdateProfileHandler, GetProfilePermissionsHandler y
/// GetProfilePermissionAuditHandler reciben un ProfileId externo y lo resuelven vía
/// GetProfileByIdAsync(tenantId, profileId), tenant-scoped por contrato. Estos tests prueban que
/// un ProfileId que pertenece a OTRO tenant (id válido, pero fuera del tenant activo del caller)
/// nunca se resuelve — el repo, tenant-scoped, devuelve null y el handler falla sin leer ni
/// mutar nada. GetProfilesHandler y CreateProfileHandler no reciben ningún id externo (ambos
/// operan enteramente sobre el TenantId ambiental de ICurrentTenant) por lo que no tienen
/// superficie de ataque cross-tenant que probar aquí.
/// </summary>
public sealed class ProfilesTenantScopeTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task UpdateProfile_perfil_de_otro_tenant_falla_sin_actualizar_ni_invalidar_cache()
    {
        var repo = new Mock<IAccessRepository>();
        var tenant = new Mock<ICurrentTenant>();
        var user = new Mock<ICurrentUser>();
        var permissionsCache = new Mock<IPermissionsCacheInvalidator>();
        var navigationBuilder = new Mock<INavigationBuilder>();

        tenant.Setup(t => t.TenantId).Returns(TenantId);
        user.Setup(u => u.UserId).Returns(UserId);

        var profileOfAnotherTenant = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(TenantId, profileOfAnotherTenant, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = new UpdateProfileHandler(
            repo.Object,
            tenant.Object,
            user.Object,
            permissionsCache.Object,
            navigationBuilder.Object
        );

        var result = await handler.Handle(
            new UpdateProfileCommand(profileOfAnotherTenant, "Nuevo nombre", null, true),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Perfil no encontrado");
        repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        permissionsCache.Verify(
            c => c.BumpCompanyVersionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        navigationBuilder.Verify(
            n => n.InvalidateCache(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetProfilePermissions_perfil_de_otro_tenant_falla_sin_leer_permisos()
    {
        var repo = new Mock<IAccessRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var profileOfAnotherTenant = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(TenantId, profileOfAnotherTenant, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = new GetProfilePermissionsHandler(repo.Object, tenant.Object);

        var result = await handler.Handle(
            new GetProfilePermissionsQuery(profileOfAnotherTenant),
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
    public async Task GetProfilePermissionAudit_perfil_de_otro_tenant_devuelve_NotFound_sin_leer_permisos()
    {
        var repo = new Mock<IAccessRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var profileOfAnotherTenant = Guid.NewGuid();
        repo.Setup(r =>
                r.GetProfileByIdAsync(TenantId, profileOfAnotherTenant, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = new GetProfilePermissionAuditHandler(repo.Object, tenant.Object);

        var result = await handler.Handle(
            new GetProfilePermissionAuditQuery(profileOfAnotherTenant),
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
    public async Task GetProfilePermissions_perfil_del_propio_tenant_se_resuelve_correctamente()
    {
        var repo = new Mock<IAccessRepository>();
        var tenant = new Mock<ICurrentTenant>();
        tenant.Setup(t => t.TenantId).Returns(TenantId);

        var profileId = Guid.NewGuid();
        var profile = AccessProfile.Create(TenantId, "Test", null, UserId);
        repo.Setup(r => r.GetProfileByIdAsync(TenantId, profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repo.Setup(r =>
                r.GetProfilePermissionsAsync(TenantId, profileId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(Array.Empty<AccessProfilePermission>());

        var handler = new GetProfilePermissionsHandler(repo.Object, tenant.Object);

        var result = await handler.Handle(
            new GetProfilePermissionsQuery(profileId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProfileId.Should().Be(profileId);
    }
}

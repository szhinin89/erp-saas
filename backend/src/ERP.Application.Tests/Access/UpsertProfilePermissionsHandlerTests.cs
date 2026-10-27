using ERP.Application.Access;
using ERP.Application.Access.Caching;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Security;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 / SECURITY-PERMISSION-SCOPE-01 — cubre: (1) rechazo atómico
/// de permisos inexistentes y no asignables contra KernelRegistry (nunca un guardado parcial de
/// los válidos), y (2) la regla anti-escalamiento: un asignador sin rol Admin nunca puede otorgar
/// un permiso que él mismo no tiene efectivo en su contexto operativo actual. SEC-05: el perfil se
/// resuelve en el tenant y la empresa activa; un perfil de otra empresa no existe para el handler y
/// la invalidación de cachés solo alcanza a la empresa activa.
/// </summary>
public sealed class UpsertProfilePermissionsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid AssignerProfileId = Guid.NewGuid();

    private readonly Mock<IAccessRepository> _repo = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<ICurrentCompany> _currentCompany = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IPermissionsCacheInvalidator> _permissionsCache = new();
    private readonly Mock<INavigationBuilder> _navigationBuilder = new();
    private readonly Mock<ICompanyContextProvider> _companyContext = new();
    private readonly Mock<IEffectivePermissionKeysProvider> _effectivePermissionKeys = new();

    private UpsertProfilePermissionsHandler CreateHandler() =>
        new(
            _repo.Object,
            _currentTenant.Object,
            _currentCompany.Object,
            _currentUser.Object,
            _permissionsCache.Object,
            _navigationBuilder.Object,
            _companyContext.Object,
            _effectivePermissionKeys.Object
        );

    public UpsertProfilePermissionsHandlerTests()
    {
        _currentTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _currentCompany.SetupGet(x => x.CompanyId).Returns(CompanyId);
        _currentCompany.SetupGet(x => x.HasCompanyContext).Returns(true);
        _currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(x => x.UserId).Returns(UserId);

        _repo
            .Setup(x =>
                x.GetProfileByIdAsync(TenantId, CompanyId, ProfileId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(AccessProfile.Create(TenantId, CompanyId, "Test", null, UserId));
        _repo
            .Setup(x =>
                x.GetProfilePermissionAsync(
                    TenantId,
                    ProfileId,
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AccessProfilePermission?)null);
        _repo
            .Setup(x =>
                x.GetCompanyUserMembershipsByCompanyAsync(
                    CompanyId,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<CompanyUserMembership>());
    }

    // SEC-04: el rol que cuenta es el de la membership en la empresa operativa (el JWT lo replica
    // aquí solo como dato de sesión, no como autoridad).
    private void SetAssignerRole(string? role)
    {
        _currentUser.SetupGet(x => x.Role).Returns(role);
        if (role == SecurityRoles.Admin)
            _companyContext
                .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new OperationalCompanyContext(
                        CompanyId,
                        UserId,
                        null,
                        true,
                        MembershipRole: SecurityRoles.Admin
                    )
                );
    }

    private void SetAssignerEffectivePermissions(params string[] keys)
    {
        _companyContext
            .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalCompanyContext(
                    CompanyId,
                    UserId,
                    AssignerProfileId,
                    true,
                    MembershipRole: "User"
                )
            );
        _effectivePermissionKeys
            .Setup(x =>
                x.GetAllowedKeysAsync(
                    TenantId,
                    CompanyId,
                    UserId,
                    AssignerProfileId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(keys);
    }

    [Fact]
    public async Task Unknown_permission_key_is_rejected_atomically_with_ValidationError_and_nothing_is_saved()
    {
        SetAssignerRole(SecurityRoles.Admin);
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[]
            {
                new PermissionUpsertItem("access.profiles.view", true), // real
                new PermissionUpsertItem("totally.made.up.permission", true), // unknown
            }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain("totally.made.up.permission");

        _repo.Verify(
            x =>
                x.GetProfileByIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_assignable_permission_key_is_rejected_with_ValidationError_and_nothing_is_saved()
    {
        var nonAssignableKey = KernelRegistry
            .Permissions.Except(KernelRegistry.AssignablePermissionKeys, StringComparer.Ordinal)
            .FirstOrDefault();
        if (nonAssignableKey is null)
            return; // No hay hoy permisos declarados-pero-no-asignables en el Kernel Registry.

        SetAssignerRole(SecurityRoles.Admin);
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem(nonAssignableKey, true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.ValidationError);
        result.Error.Should().Contain(nonAssignableKey);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Known_permission_keys_are_saved_successfully_when_assigner_is_Admin()
    {
        SetAssignerRole(SecurityRoles.Admin);
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().ContainSingle().Which.Should().Be("access.profiles.view");
        result.Value.Rejected.Should().BeEmpty();
        _repo.Verify(
            x =>
                x.AddProfilePermissionAsync(
                    It.IsAny<AccessProfilePermission>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Admin de la empresa operativa omite el chequeo de alcance (no consulta sus claves).
        _effectivePermissionKeys.Verify(
            x =>
                x.GetAllowedKeysAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Jwt_Admin_whose_operational_company_membership_is_not_Admin_cannot_escalate()
    {
        // SEC-04: JWT emitido como Admin en A; la empresa operativa (X-Company-Id = B) tiene
        // membership "User" con perfil limitado → se aplica la regla anti-escalamiento de B.
        _currentUser.SetupGet(x => x.Role).Returns(SecurityRoles.Admin);
        SetAssignerEffectivePermissions("access.profiles.view");
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.sessions.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        result.Error.Should().Contain("access.sessions.view");
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Jwt_Admin_without_resolvable_operational_context_is_forbidden()
    {
        _currentUser.SetupGet(x => x.Role).Returns(SecurityRoles.Admin);
        _companyContext
            .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationalCompanyContext?)null);
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
    }

    [Fact]
    public async Task Authorized_global_operator_can_grant_without_membership()
    {
        _currentUser.SetupGet(x => x.Role).Returns(SecurityRoles.Admin);
        _companyContext
            .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalCompanyContext(
                    CompanyId,
                    UserId,
                    null,
                    false,
                    MembershipRole: SecurityRoles.Admin,
                    IsAuthorizedOperator: true
                )
            );
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.sessions.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Non_admin_assigner_cannot_grant_a_permission_outside_their_own_effective_scope()
    {
        SetAssignerRole("User");
        SetAssignerEffectivePermissions("access.profiles.view"); // assigner lacks access.sessions.view
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.sessions.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        result.Error.Should().Contain("access.sessions.view");
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Non_admin_assigner_can_grant_a_permission_they_effectively_have()
    {
        SetAssignerRole("User");
        SetAssignerEffectivePermissions("access.profiles.view");
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().ContainSingle().Which.Should().Be("access.profiles.view");
    }

    [Fact]
    public async Task Non_admin_assigner_can_revoke_a_permission_they_do_not_have_themselves()
    {
        SetAssignerRole("User");
        // Assigner has no effective permissions at all — but revoking never escalates privilege.
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", false) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _companyContext.Verify(
            x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task Profile_belonging_to_another_company_is_rejected_as_not_found_and_nothing_is_saved()
    {
        // SEC-05: GetProfileByIdAsync resuelve tenant + empresa activa + profileId — un perfil de
        // OTRA empresa (mismo tenant) o de otro tenant nunca se resuelve, aunque el ProfileId sea
        // válido o adivinable, ni siquiera para un Admin de la empresa activa.
        SetAssignerRole(SecurityRoles.Admin);
        var profileOfAnotherCompany = Guid.NewGuid();
        _repo
            .Setup(x =>
                x.GetProfileByIdAsync(
                    TenantId,
                    CompanyId,
                    profileOfAnotherCompany,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((AccessProfile?)null);

        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            profileOfAnotherCompany,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Perfil no existe");
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(
            x =>
                x.AddProfilePermissionAsync(
                    It.IsAny<AccessProfilePermission>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Non_admin_assigner_without_resolvable_operational_context_is_forbidden()
    {
        SetAssignerRole("User");
        _companyContext
            .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((OperationalCompanyContext?)null);
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Assigner_context_resolved_in_another_company_is_forbidden()
    {
        // SEC-05: el alcance del asignador se evalúa en la misma empresa del perfil (empresa
        // activa); un contexto operativo de otra empresa nunca autoriza la concesión.
        _companyContext
            .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new OperationalCompanyContext(
                    Guid.NewGuid(),
                    UserId,
                    null,
                    true,
                    MembershipRole: SecurityRoles.Admin
                )
            );
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cache_invalidation_only_reaches_the_active_company_memberships_of_the_profile()
    {
        SetAssignerRole(SecurityRoles.Admin);
        var memberUserId = Guid.NewGuid();
        _repo
            .Setup(x =>
                x.GetCompanyUserMembershipsByCompanyAsync(
                    CompanyId,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                [
                    CompanyUserMembership.Create(
                        CompanyId,
                        memberUserId,
                        "User",
                        ProfileId,
                        UserId
                    ),
                    CompanyUserMembership.Create(CompanyId, Guid.NewGuid(), "User", null, UserId),
                ]
            );
        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _permissionsCache.Verify(
            x => x.BumpCompanyVersionAsync(CompanyId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _permissionsCache.Verify(
            x =>
                x.BumpCompanyVersionAsync(
                    It.Is<Guid>(id => id != CompanyId),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
        _navigationBuilder.Verify(
            x => x.InvalidateCache(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()),
            Times.Once
        );
        _navigationBuilder.Verify(
            x => x.InvalidateCache(TenantId, CompanyId, memberUserId),
            Times.Once
        );
    }
}

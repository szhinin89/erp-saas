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
/// un permiso que él mismo no tiene efectivo en su contexto operativo actual.
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
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IPermissionsCacheInvalidator> _permissionsCache = new();
    private readonly Mock<INavigationBuilder> _navigationBuilder = new();
    private readonly Mock<ICompanyContextProvider> _companyContext = new();
    private readonly Mock<IEffectivePermissionKeysProvider> _effectivePermissionKeys = new();

    private UpsertProfilePermissionsHandler CreateHandler() =>
        new(
            _repo.Object,
            _currentTenant.Object,
            _currentUser.Object,
            _permissionsCache.Object,
            _navigationBuilder.Object,
            _companyContext.Object,
            _effectivePermissionKeys.Object
        );

    public UpsertProfilePermissionsHandlerTests()
    {
        _currentTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(x => x.UserId).Returns(UserId);

        _repo
            .Setup(x => x.GetProfileByIdAsync(TenantId, ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessProfile.Create(TenantId, "Test", null, UserId));
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
                x.GetCompanyUserMembershipsByTenantAsync(
                    TenantId,
                    true,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(Array.Empty<CompanyUserMembership>());
    }

    private void SetAssignerRole(string? role) =>
        _currentUser.SetupGet(x => x.Role).Returns(role);

    private void SetAssignerEffectivePermissions(params string[] keys)
    {
        _companyContext
            .Setup(x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalCompanyContext(CompanyId, UserId, AssignerProfileId, true));
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
            x => x.GetProfileByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
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
            x => x.AddProfilePermissionAsync(It.IsAny<AccessProfilePermission>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Admin bypasses the anti-escalation check entirely.
        _companyContext.Verify(
            x => x.ResolveOperationalForCurrentUserAsync(It.IsAny<CancellationToken>()),
            Times.Never
        );
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
}

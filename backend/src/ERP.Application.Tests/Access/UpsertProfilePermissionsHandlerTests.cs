using ERP.Application.Access.Caching;
using ERP.Application.Access.UseCases.Permissions;
using ERP.Application.Common;
using ERP.Application.Navigation;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// ADMIN-PERMISSIONS-SSOT-KERNEL-02 — antes de este ticket no existía ningún test para este
/// handler (gap real cerrado aquí). Foco: rechazo atómico de permisos desconocidos contra
/// KernelRegistry.AssignablePermissionKeys — nunca un guardado parcial de los válidos.
/// </summary>
public sealed class UpsertProfilePermissionsHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();

    private readonly Mock<IAccessRepository> _repo = new();
    private readonly Mock<ICurrentTenant> _currentTenant = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly Mock<IPermissionsCacheInvalidator> _permissionsCache = new();
    private readonly Mock<INavigationBuilder> _navigationBuilder = new();

    private UpsertProfilePermissionsHandler CreateHandler() =>
        new(
            _repo.Object,
            _currentTenant.Object,
            _currentUser.Object,
            _permissionsCache.Object,
            _navigationBuilder.Object
        );

    public UpsertProfilePermissionsHandlerTests()
    {
        _currentTenant.SetupGet(x => x.TenantId).Returns(TenantId);
        _currentUser.SetupGet(x => x.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(x => x.UserId).Returns(UserId);
    }

    [Fact]
    public async Task Unknown_permission_key_is_rejected_atomically_with_ValidationError_and_nothing_is_saved()
    {
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
    public async Task Known_permission_keys_are_saved_successfully()
    {
        var profile = AccessProfile.Create(TenantId, "Test", null, UserId);
        _repo
            .Setup(x => x.GetProfileByIdAsync(TenantId, ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        _repo
            .Setup(x =>
                x.GetProfilePermissionAsync(
                    TenantId,
                    ProfileId,
                    "access.profiles.view",
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

        var handler = CreateHandler();
        var command = new UpsertProfilePermissionsCommand(
            ProfileId,
            new[] { new PermissionUpsertItem("access.profiles.view", true) }
        );

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Saved.Should().ContainSingle().Which.Should().Be("access.profiles.view");
        result.Value.Rejected.Should().BeEmpty();
        _repo.Verify(x => x.AddProfilePermissionAsync(It.IsAny<AccessProfilePermission>(), It.IsAny<CancellationToken>()), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

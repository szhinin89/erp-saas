using ERP.Application.Access.UseCases.LookupUserByUsernameAdmin;
using ERP.Application.Common;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Fase F (renombrado a username en Fase G): único endpoint que le permite al frontend decidir
/// automáticamente entre "asignar membership" (usuario existente) y "crear usuario" (nuevo) sin
/// que el administrador tenga que saberlo de antemano. Solo compone
/// IAccessRepository.GetUserByUsernameAsync + GetCompanyUserMembershipAsync — no reimplementa
/// ninguna regla de negocio.
/// </summary>
public sealed class LookupUserByUsernameAdminHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string Username = "ana.perez";

    private static IdentityUser NewUser() =>
        IdentityUser.Create(Username, "Ana", "Perez", "ana@test.com", "hash", CreatedBy);

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public CurrentCompanyStub(bool hasContext = true) => HasCompanyContext = hasContext;

        public Guid CompanyId => LookupUserByUsernameAdminHandlerTests.CompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext { get; }
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();

        public LookupUserByUsernameAdminHandler BuildHandler(bool hasCompanyContext = true) =>
            new(AccessRepo.Object, new CurrentCompanyStub(hasCompanyContext));
    }

    [Fact]
    public async Task Sin_empresa_activa_devuelve_Forbidden()
    {
        var f = new Fixture();
        var handler = f.BuildHandler(hasCompanyContext: false);

        var result = await handler.Handle(
            new LookupUserByUsernameAdminQuery(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
    }

    [Fact]
    public async Task Username_inexistente_devuelve_IdentityUserExists_false()
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityUser?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LookupUserByUsernameAdminQuery(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IdentityUserExists.Should().BeFalse();
        result.Value.Membership.Should().BeNull();
        f.AccessRepo.Verify(
            r =>
                r.GetCompanyUserMembershipAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Usuario_existente_sin_membership_devuelve_FullName_y_Membership_null()
    {
        var user = NewUser();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LookupUserByUsernameAdminQuery(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.IdentityUserExists.Should().BeTrue();
        result.Value.FullName.Should().Be(user.FullName);
        result.Value.Membership.Should().BeNull();
    }

    [Fact]
    public async Task Usuario_existente_con_membership_activa_devuelve_su_estado()
    {
        var user = NewUser();
        var profileId = Guid.NewGuid();
        var membership = CompanyUserMembership.Create(
            CompanyId,
            user.Id,
            "Admin",
            profileId,
            CreatedBy
        );
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LookupUserByUsernameAdminQuery(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Membership.Should().NotBeNull();
        result.Value.Membership!.IsActive.Should().BeTrue();
        result.Value.Membership.Role.Should().Be("Admin");
        result.Value.Membership.ProfileId.Should().Be(profileId);
    }

    [Fact]
    public async Task Usuario_existente_con_membership_revocada_devuelve_IsActive_false()
    {
        var user = NewUser();
        var membership = CompanyUserMembership.Create(CompanyId, user.Id, "User", null, CreatedBy);
        membership.Deactivate(CreatedBy);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.AccessRepo.Setup(r =>
                r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new LookupUserByUsernameAdminQuery(Username),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        result.Value!.Membership!.IsActive.Should().BeFalse();
    }
}

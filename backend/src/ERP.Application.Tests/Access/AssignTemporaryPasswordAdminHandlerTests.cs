using ERP.Application.Access.UseCases.AssignTemporaryPasswordAdmin;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>
/// Caso de uso administrativo "Asignar contraseña temporal". Cubre: resolución del usuario
/// objetivo por Username, scoping anti-IDOR contra la empresa activa (mismo criterio que
/// LookupUserByUsernameAdminHandler), composición de IdentityUser.SetPasswordHash +
/// MarkRequirePasswordReset (dominio, sin reimplementar hashing/stamp), delegación íntegra en
/// IUserAccessRevocationService, y atomicidad vía IUnitOfWork.
/// </summary>
public sealed class AssignTemporaryPasswordAdminHandlerTests
{
    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private const string Username = "ana.perez";
    private const string TemporaryPassword = "Temp0ral!";

    private static IdentityUser NewTargetUser() =>
        IdentityUser.Create(Username, "Ana", "Perez", "ana@test.com", "old-hash", AdminUserId);

    private static CompanyUserMembership ActiveMembership(Guid identityUserId) =>
        CompanyUserMembership.Create(CompanyId, identityUserId, "User", null, AdminUserId);

    private sealed class CurrentUserStub : ICurrentUser
    {
        public Guid UserId => AdminUserId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public Guid TenantId => AssignTemporaryPasswordAdminHandlerTests.TenantId;
        public string? Slug => null;
    }

    private sealed class CurrentCompanyStub : ICurrentCompany
    {
        public Guid CompanyId => AssignTemporaryPasswordAdminHandlerTests.CompanyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IUserAccessRevocationService> RevocationService { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();

        public Fixture()
        {
            Hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("new-temp-hash");
        }

        public AssignTemporaryPasswordAdminHandler BuildHandler() => new(
            AccessRepo.Object, Hasher.Object, RevocationService.Object, UnitOfWork.Object,
            new CurrentUserStub(), new CurrentTenantStub(), new CurrentCompanyStub());
    }

    private static AssignTemporaryPasswordAdminCommand ValidCommand() =>
        new(Username, TemporaryPassword);

    [Fact]
    public async Task Asignacion_correcta_devuelve_Success_sin_exponer_la_contrasena()
    {
        var user = NewTargetUser();
        var membership = ActiveMembership(user.Id);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain(TemporaryPassword).And.NotContain("new-temp-hash");
        f.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Usuario_inexistente_devuelve_NotFound_y_no_abre_transaccion()
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync((IdentityUser?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.NotFound);
        f.AccessRepo.Verify(r => r.GetCompanyUserMembershipAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        f.UnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Usuario_sin_membership_en_la_empresa_activa_devuelve_Forbidden()
    {
        var user = NewTargetUser();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompanyUserMembership?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        user.PasswordHash.Should().Be("old-hash", "no debe tocarse el password si el scoping de empresa falla");
        f.UnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.RevocationService.Verify(
            s => s.RevokeAllAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Membership_inactiva_devuelve_Forbidden()
    {
        var user = NewTargetUser();
        var membership = ActiveMembership(user.Id);
        membership.Deactivate(AdminUserId);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        var result = await handler.Handle(ValidCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.Forbidden);
        user.PasswordHash.Should().Be("old-hash");
        f.UnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Asignacion_correcta_invoca_SetPasswordHash_con_el_hash_generado()
    {
        var user = NewTargetUser();
        var membership = ActiveMembership(user.Id);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        await handler.Handle(ValidCommand(), CancellationToken.None);

        f.Hasher.Verify(h => h.HashPassword(TemporaryPassword), Times.Once);
        user.PasswordHash.Should().Be("new-temp-hash");
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Asignacion_correcta_invoca_MarkRequirePasswordReset_dejando_el_flag_activo()
    {
        var user = NewTargetUser();
        var membership = ActiveMembership(user.Id);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        await handler.Handle(ValidCommand(), CancellationToken.None);

        user.RequirePasswordReset.Should().BeTrue("el usuario debe cambiar la contraseña temporal en su próximo login");
    }

    [Fact]
    public async Task Asignacion_correcta_revoca_todo_el_acceso_del_usuario_objetivo_con_el_actor_correcto()
    {
        var user = NewTargetUser();
        var membership = ActiveMembership(user.Id);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);

        var handler = f.BuildHandler();
        await handler.Handle(ValidCommand(), CancellationToken.None);

        f.RevocationService.Verify(
            s => s.RevokeAllAccessAsync(
                user.Id, TenantId, AdminUserId,
                "Contraseña temporal asignada por administrador", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Falla_en_la_revocacion_hace_Rollback_y_propaga_la_excepcion()
    {
        var user = NewTargetUser();
        var membership = ActiveMembership(user.Id);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.AccessRepo.Setup(r => r.GetCompanyUserMembershipAsync(CompanyId, user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(membership);
        f.RevocationService
            .Setup(s => s.RevokeAllAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var handler = f.BuildHandler();
        var act = () => handler.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        f.UnitOfWork.Verify(u => u.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

using ERP.Application.Auth.UseCases.ChangeMyPassword;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// Fase E: primer flujo de cambio de contraseña para un usuario ya autenticado (sobre sí mismo).
/// Espeja ResetPasswordWithTokenHandler (misma aplicación vía IdentityUser.SetPasswordHash, misma
/// invalidación vía IRefreshTokenService.RevokeAllForUserAsync) pero exige la contraseña actual en
/// vez de un token, sin tocar el flujo de recuperación anónimo existente.
/// </summary>
public sealed class ChangeMyPasswordHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static IdentityUser NewUser() =>
        IdentityUser.Create("ana.perez", "Ana", "Perez", "ana@test.com", "old-hash", UserId);

    private sealed class CurrentUserStub : ICurrentUser
    {
        public Guid UserId => ChangeMyPasswordHandlerTests.UserId;
        public bool IsAuthenticated => true;
        public string? Username => null;
        public string? Email => null;
        public string? FullName => null;
        public string? Role => null;
    }

    private sealed class CurrentTenantStub : ICurrentTenant
    {
        public Guid TenantId => ChangeMyPasswordHandlerTests.TenantId;
        public string? Slug => null;
    }

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();

        public ChangeMyPasswordHandler BuildHandler() =>
            new(
                AccessRepo.Object,
                Hasher.Object,
                new CurrentUserStub(),
                new CurrentTenantStub(),
                RefreshTokenService.Object
            );
    }

    [Fact]
    public async Task Contrasena_actual_incorrecta_devuelve_Failure_sin_mutar_ni_revocar_nada()
    {
        var user = NewUser();
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.Hasher.Setup(h => h.VerifyPassword("wrong", "old-hash")).Returns(false);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ChangeMyPasswordCommand("wrong", "N3wPassword!"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("La contraseña actual no es correcta.");
        user.PasswordHash.Should().Be("old-hash");
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.RefreshTokenService.Verify(
            s =>
                s.RevokeAllForUserAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task Contrasena_actual_correcta_aplica_el_hash_nuevo_limpia_RequirePasswordReset_y_revoca_todas_las_sesiones()
    {
        var user = NewUser();
        user.MarkRequirePasswordReset(UserId);
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.Hasher.Setup(h => h.VerifyPassword("old-pass", "old-hash")).Returns(true);
        f.Hasher.Setup(h => h.HashPassword("N3wPassword!")).Returns("new-hash");

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ChangeMyPasswordCommand("old-pass", "N3wPassword!"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("new-hash");
        user.RequirePasswordReset.Should().BeFalse();
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        f.RefreshTokenService.Verify(
            s =>
                s.RevokeAllForUserAsync(
                    user.Id,
                    TenantId,
                    "Cambio de contraseña (self-service)",
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Usuario_no_encontrado_devuelve_Failure()
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IdentityUser?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(
            new ChangeMyPasswordCommand("old-pass", "N3wPassword!"),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
    }
}

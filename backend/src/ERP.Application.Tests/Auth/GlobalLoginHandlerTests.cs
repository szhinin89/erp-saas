using ERP.Application.Auth.UseCases.GlobalLogin;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01 — login global sin enumeración de usuarios: mismo
/// fallo y una verificación de contraseña (real o simulada) en cada camino; el estado inactivo solo
/// se revela tras una contraseña correcta.
/// </summary>
public sealed class GlobalLoginHandlerTests
{
    private const string Username = "admin.global";
    private const string Password = "Sup3rSecret!";
    private const string PasswordHash = "hashed-password";

    private sealed class Fixture
    {
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IAccessTokenService> TokenService { get; } = new();
        public Mock<IPasswordHasher> PasswordHasher { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();

        public GlobalLoginHandler Build() =>
            new(
                AccessRepo.Object,
                TokenService.Object,
                PasswordHasher.Object,
                RefreshTokenService.Object
            );
    }

    private static async Task<(
        ERP.Application.Common.Result<ERP.Application.Auth.DTOs.AuthResponseDto> Result,
        Fixture F
    )> Login(IdentityUser? user, bool passwordOk)
    {
        var f = new Fixture();
        f.AccessRepo.Setup(r => r.GetUserByUsernameAsync(Username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        f.PasswordHasher.Setup(h => h.VerifyPassword(Password, PasswordHash)).Returns(passwordOk);
        return (
            await f.Build()
                .Handle(new GlobalLoginCommand(Username, Password), CancellationToken.None),
            f
        );
    }

    private static IdentityUser NewUser(bool active = true)
    {
        var user = IdentityUser.Create(
            Username,
            "Admin",
            "Global",
            "admin@test.com",
            PasswordHash,
            Guid.NewGuid()
        );
        if (!active)
            user.Deactivate(Guid.NewGuid());
        return user;
    }

    [Fact]
    public async Task Inexistente_contrasena_incorrecta_e_inactivo_sin_contrasena_son_indistinguibles_y_verifican_una_vez()
    {
        var (nonexistent, fNone) = await Login(null, passwordOk: false);
        var (wrong, fWrong) = await Login(NewUser(), passwordOk: false);
        var (inactiveWrong, fInactive) = await Login(NewUser(active: false), passwordOk: false);

        nonexistent.Error.Should().Be("Credenciales inválidas.");
        (wrong.Error, wrong.Code).Should().Be((nonexistent.Error, nonexistent.Code));
        (inactiveWrong.Error, inactiveWrong.Code)
            .Should()
            .Be((nonexistent.Error, nonexistent.Code));
        fNone.PasswordHasher.Verify(h => h.SimulatePasswordVerification(Password), Times.Once);
        fWrong.PasswordHasher.Verify(h => h.VerifyPassword(Password, PasswordHash), Times.Once);
        fInactive.PasswordHasher.Verify(h => h.VerifyPassword(Password, PasswordHash), Times.Once);
    }

    [Fact]
    public async Task Inactivo_con_contrasena_correcta_conserva_Usuario_inactivo()
    {
        var (result, _) = await Login(NewUser(active: false), passwordOk: true);

        result.Error.Should().Be("Usuario inactivo.");
    }
}

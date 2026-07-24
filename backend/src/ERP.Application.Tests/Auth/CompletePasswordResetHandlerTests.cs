using ERP.Application.Auth.DTOs;
using ERP.Application.Auth.UseCases.CompletePasswordReset;
using ERP.Application.Auth.UseCases.Login;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// Fase H — CompletePasswordResetHandler no reimplementa nada de la emisión de sesión: una vez
/// que el token se consume y la contraseña queda establecida, delega íntegro en LoginCommand vía
/// IMediator (mismo patrón que otros handlers que delegan en el "comando real" — ver
/// UpsertCompanyUserMembershipHandler). Estos tests cubren solo el agregado propio: validación de
/// token, aplicación de la nueva contraseña, y la delegación — no la lógica de LoginHandler
/// (cubierta aparte en LoginHandlerTests).
/// </summary>
public sealed class CompletePasswordResetHandlerTests
{
    private static readonly Guid CreatedBy = Guid.NewGuid();
    private const string Username = "ana.perez";
    private const string RawToken = "raw-token-value";
    private const string NewPassword = "N3wPassword!";

    private static IdentityUser NewUser()
    {
        var user = IdentityUser.Create(Username, "Ana", "Perez", "ana@test.com", "old-hash", CreatedBy);
        user.MarkRequirePasswordReset(CreatedBy);
        return user;
    }

    private sealed class Fixture
    {
        public Mock<IPasswordResetTokenRepository> TokenRepo { get; } = new();
        public Mock<IAccessRepository> AccessRepo { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokenService { get; } = new();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IValidator<CompletePasswordResetCommand>> Validator { get; } = new();

        public Fixture()
        {
            Validator.Setup(v => v.ValidateAsync(It.IsAny<CompletePasswordResetCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ValidationResult());
        }

        public CompletePasswordResetHandler BuildHandler() => new(
            TokenRepo.Object, AccessRepo.Object, Hasher.Object, RefreshTokenService.Object,
            Mediator.Object, Validator.Object);
    }

    private static PasswordResetToken StoredToken(Guid userId, string hash) =>
        PasswordResetToken.Create(hash, userId, PasswordResetToken.KindIdentity, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(5));

    [Fact]
    public async Task Token_inexistente_devuelve_Failure_sin_tocar_nada()
    {
        var f = new Fixture();
        f.TokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PasswordResetToken?)null);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new CompletePasswordResetCommand(RawToken, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.AccessRepo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        f.Mediator.Verify(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Token_ya_usado_devuelve_Failure()
    {
        var user = NewUser();
        var f = new Fixture();
        var stored = StoredToken(user.Id, "irrelevant-hash");
        stored.MarkUsed();
        f.TokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new CompletePasswordResetCommand(RawToken, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.Mediator.Verify(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Token_valido_aplica_la_nueva_contrasena_limpia_RequirePasswordReset_y_delega_en_LoginCommand()
    {
        var user = NewUser();
        var f = new Fixture();
        var stored = StoredToken(user.Id, "irrelevant-hash");
        f.TokenRepo.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
        f.AccessRepo.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.Hasher.Setup(h => h.HashPassword(NewPassword)).Returns("new-hash");

        var expectedResponse = Result<AuthResponseDto>.Success(
            new AuthResponseDto(user.Id, user.FullName, user.Username, user.Email?.Value, "Admin", Guid.NewGuid(), "jwt-token"));
        LoginCommand? sentCommand = null;
        f.Mediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<AuthResponseDto>>, CancellationToken>((cmd, _) => sentCommand = (LoginCommand)cmd)
            .ReturnsAsync(expectedResponse);

        var handler = f.BuildHandler();
        var result = await handler.Handle(new CompletePasswordResetCommand(RawToken, NewPassword), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("jwt-token");

        user.PasswordHash.Should().Be("new-hash");
        user.RequirePasswordReset.Should().BeFalse();
        stored.Used.Should().BeTrue();

        f.RefreshTokenService.Verify(
            s => s.RevokeAllForUserAsync(user.Id, It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        sentCommand.Should().NotBeNull();
        sentCommand!.Username.Should().Be(user.Username);
        sentCommand.Password.Should().Be(NewPassword);
    }

    [Fact]
    public async Task Validacion_fallida_no_consulta_el_repositorio_de_tokens()
    {
        var f = new Fixture();
        f.Validator.Setup(v => v.ValidateAsync(It.IsAny<CompletePasswordResetCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(new[] { new ValidationFailure("NewPassword", "La nueva contraseña es obligatoria.") }));

        var handler = f.BuildHandler();
        var result = await handler.Handle(new CompletePasswordResetCommand(RawToken, ""), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        f.TokenRepo.Verify(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

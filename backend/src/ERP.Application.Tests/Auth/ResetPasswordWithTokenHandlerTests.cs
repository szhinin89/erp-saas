using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Tenants.Interfaces;
using FluentAssertions;
using Moq;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Application.Tests.Auth;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — regresión del consumo del token (sin cambios de
/// comportamiento): válido aplica la contraseña y queda usado; usado o expirado se rechazan sin
/// tocar al usuario.
/// </summary>
public sealed class ResetPasswordWithTokenHandlerTests
{
    private const string RawToken = "raw-reset-token";
    private const string NewPassword = "N3wPassword!2026";
    private static readonly Guid Actor = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<IPasswordResetTokenRepository> Tokens { get; } = new();
        public Mock<IAccessRepository> Access { get; } = new();
        public Mock<ITenantRepository> Tenants { get; } = new();
        public Mock<IPasswordHasher> Hasher { get; } = new();
        public Mock<IRefreshTokenService> RefreshTokens { get; } = new();

        public void GivenStored(PasswordResetToken token) =>
            Tokens
                .Setup(r => r.GetByTokenHashAsync(HashOf(RawToken), It.IsAny<CancellationToken>()))
                .ReturnsAsync(token);

        public ResetPasswordWithTokenHandler Build() =>
            new(
                Tokens.Object,
                Access.Object,
                Tenants.Object,
                Hasher.Object,
                RefreshTokens.Object,
                new ResetPasswordWithTokenCommandValidator()
            );
    }

    private static string HashOf(string raw) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

    private static PasswordResetToken Token(Guid userId, Guid tenantId, DateTime expiresAtUtc) =>
        PasswordResetToken.Create(HashOf(RawToken), userId, PasswordResetToken.KindIdentity, tenantId, expiresAtUtc);

    // H
    [Fact]
    public async Task Token_valido_aplica_la_contrasena_revoca_sesiones_y_marca_usado()
    {
        var f = new Fixture();
        var user = IdentityUser.Create("ana.perez", "Ana", "Perez", "ana@test.com", "old-hash", Actor);
        var tenantId = Guid.NewGuid();
        var stored = Token(user.Id, tenantId, DateTime.UtcNow.AddMinutes(30));
        f.GivenStored(stored);
        f.Access.Setup(r => r.GetUserByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        f.Hasher.Setup(h => h.HashPassword(NewPassword)).Returns("new-hash");

        var result = await f.Build().Handle(
            new ResetPasswordWithTokenCommand(RawToken, NewPassword, tenantId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeTrue();
        stored.Used.Should().BeTrue();
        f.RefreshTokens.Verify(
            r => r.RevokeAllForUserAsync(user.Id, tenantId, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    // I
    [Fact]
    public async Task Token_usado_se_rechaza()
    {
        var f = new Fixture();
        var tenantId = Guid.NewGuid();
        var stored = Token(Guid.NewGuid(), tenantId, DateTime.UtcNow.AddMinutes(30));
        stored.MarkUsed();
        f.GivenStored(stored);

        var result = await f.Build().Handle(
            new ResetPasswordWithTokenCommand(RawToken, NewPassword, tenantId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ResetPasswordWithTokenHandler.InvalidTokenMessage);
        f.Access.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // J
    [Fact]
    public async Task Token_expirado_se_rechaza()
    {
        var f = new Fixture();
        var tenantId = Guid.NewGuid();
        f.GivenStored(Token(Guid.NewGuid(), tenantId, DateTime.UtcNow.AddSeconds(-1)));

        var result = await f.Build().Handle(
            new ResetPasswordWithTokenCommand(RawToken, NewPassword, tenantId),
            CancellationToken.None
        );

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(ResetPasswordWithTokenHandler.InvalidTokenMessage);
        f.Access.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

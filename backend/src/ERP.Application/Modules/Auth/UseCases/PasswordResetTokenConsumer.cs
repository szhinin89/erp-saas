using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Entities;
using ERP.Domain.Access.Interfaces;
using ERP.Domain.Auth.Entities;

namespace ERP.Application.Auth.UseCases;

/// <summary>
/// Fase H — único punto donde se consume un <see cref="PasswordResetToken"/> (buscar por hash,
/// validar vigencia/kind, aplicar la nueva contraseña, revocar sesiones, marcar usado). Antes
/// vivía inline en <c>ResetPasswordWithTokenHandler</c>; ahora también lo usa
/// <c>CompletePasswordResetHandler</c> (Fase H, primer login). El chequeo de negocio adicional
/// específico de cada flujo (ej. el match de TenantId contra un valor recibido del cliente en
/// ResetPasswordWithToken) queda en el caller — <see cref="TryGetValidAsync"/> solo valida lo
/// genérico a cualquier token.
/// </summary>
internal static class PasswordResetTokenConsumer
{
    public static Task<PasswordResetToken?> TryGetValidAsync(
        IPasswordResetTokenRepository tokenRepository,
        string rawToken,
        string expectedUserKind,
        CancellationToken cancellationToken)
    {
        var hash = PasswordResetTokenCrypto.Hash(rawToken.Trim());
        return GetIfValidAsync(tokenRepository, hash, expectedUserKind, cancellationToken);
    }

    private static async Task<PasswordResetToken?> GetIfValidAsync(
        IPasswordResetTokenRepository tokenRepository, string hash, string expectedUserKind, CancellationToken cancellationToken)
    {
        var stored = await tokenRepository.GetByTokenHashAsync(hash, cancellationToken);
        if (stored is null || !stored.IsValid || stored.UserKind != expectedUserKind)
            return null;

        return stored;
    }

    /// <summary>
    /// Aplica el cambio real: <see cref="IdentityUser.SetPasswordHash"/> + <see cref="IdentityUser.ClearRequirePasswordReset"/>
    /// (el usuario acaba de demostrar la contraseña nueva/token válido, mismo mecanismo que
    /// ChangeMyPasswordHandler), revoca todas las sesiones activas y marca el token usado.
    /// Devuelve null si el usuario del token ya no existe (mismo criterio que hoy).
    /// </summary>
    public static async Task<IdentityUser?> ApplyAsync(
        IPasswordResetTokenRepository tokenRepository,
        IAccessRepository accessRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenService refreshTokenService,
        PasswordResetToken stored,
        string newPassword,
        string revokeReason,
        CancellationToken cancellationToken)
    {
        var identity = await accessRepository.GetUserByIdAsync(stored.UserId, cancellationToken);
        if (identity is null)
            return null;

        var newHash = passwordHasher.HashPassword(newPassword);
        identity.SetPasswordHash(newHash, updatedBy: identity.Id);
        identity.ClearRequirePasswordReset(updatedBy: identity.Id);
        await accessRepository.SaveChangesAsync(cancellationToken);

        var revokeTenantId = stored.TenantId ?? Guid.Empty;
        await refreshTokenService.RevokeAllForUserAsync(identity.Id, revokeTenantId, revokeReason, cancellationToken);

        stored.MarkUsed();
        await tokenRepository.SaveChangesAsync(cancellationToken);

        return identity;
    }
}

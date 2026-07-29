using ERP.Application.Auth.UseCases.PasswordReset;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using Microsoft.Extensions.Options;

namespace ERP.Application.Auth.UseCases;

/// <summary>
/// Fase H — único punto donde se emite un <see cref="PasswordResetToken"/> (invalidar activos
/// previos del mismo usuario/kind/tenant, generar raw+hash, persistir). Antes vivía inline en
/// <c>ForgotPasswordHandler</c>; ahora también lo usa <c>LoginHandler</c> cuando
/// <c>IdentityUser.RequirePasswordReset</c> está activo — mismo mecanismo de emisión, dos
/// destinos distintos del token (enlace por email vs. respuesta directa del login).
/// </summary>
internal static class PasswordResetTokenIssuer
{
    public static async Task<(string RawToken, DateTime ExpiresAtUtc)> IssueAsync(
        IPasswordResetTokenRepository tokenRepository,
        IOptions<PasswordResetOptions> options,
        Guid userId,
        string userKind,
        Guid? tenantId,
        int? overrideLifetimeMinutes,
        CancellationToken cancellationToken
    )
    {
        await tokenRepository.InvalidateActiveForUserAsync(
            userId,
            userKind,
            tenantId,
            cancellationToken
        );

        var raw = PasswordResetTokenCrypto.CreateRawToken();
        var hash = PasswordResetTokenCrypto.Hash(raw);
        var minutes = Math.Clamp(
            overrideLifetimeMinutes ?? options.Value.TokenLifetimeMinutes,
            5,
            24 * 60
        );
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var entity = PasswordResetToken.Create(hash, userId, userKind, tenantId, expires);
        await tokenRepository.AddAsync(entity, cancellationToken);
        await tokenRepository.SaveChangesAsync(cancellationToken);

        return (raw, expires);
    }
}

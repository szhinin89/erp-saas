using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Auth.UseCases.Logout;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutHandler(IRefreshTokenService refreshTokenService)
        => _refreshTokenService = refreshTokenService;

    /// <summary>
    /// Revoca un refresh token específico (logout de un dispositivo).
    /// Si no se proporciona token, revoca todos los del usuario (logout global).
    /// </summary>
    public async Task<Result<string>> HandleAsync(
        Guid userId, Guid tenantId,
        string? rawRefreshToken,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(rawRefreshToken))
            await _refreshTokenService.RevokeAsync(rawRefreshToken, "Logout", ct);
        else
            await _refreshTokenService.RevokeAllForUserAsync(userId, tenantId, "Logout", ct);

        return Result<string>.Success("Sesión cerrada correctamente.");
    }
}

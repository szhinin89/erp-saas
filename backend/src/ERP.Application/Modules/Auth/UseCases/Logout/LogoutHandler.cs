using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Auth.UseCases.Logout;

public sealed class LogoutHandler
{
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutHandler(IRefreshTokenService refreshTokenService)
        => _refreshTokenService = refreshTokenService;

    /// <summary>
    /// Revoca el refresh token proporcionado (logout del dispositivo actual).
    /// Si <paramref name="allDevices"/> es true, primero valida el token para obtener el UserId
    /// y luego revoca TODOS los tokens activos del usuario.
    /// No requiere access token — el refresh token es la credencial de logout.
    /// </summary>
    public async Task<Result<string>> HandleAsync(
        string rawRefreshToken,
        bool allDevices = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return Result<string>.Failure("Se requiere el refresh token para cerrar sesión.");

        if (allDevices)
        {
            // Validar el token (sin rotarlo) para obtener la identidad y revocar todo
            var validation = await _refreshTokenService.ValidateAndRotateAsync(rawRefreshToken, ct);
            if (!validation.IsValid)
                return Result<string>.Failure(validation.Error ?? "Refresh token inválido.");

            // RevokeAll incluirá el token rotado recién creado — todos quedan inactivos
            await _refreshTokenService.RevokeAllForUserAsync(
                validation.UserId, validation.TenantId, "Logout global", ct);

            return Result<string>.Success("Sesión cerrada en todos los dispositivos.");
        }

        await _refreshTokenService.RevokeAsync(rawRefreshToken, "Logout", ct);
        return Result<string>.Success("Sesión cerrada correctamente.");
    }
}


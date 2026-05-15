using MediatR;
using ERP.Application.Common;
using ERP.Application.Common.Interfaces;

namespace ERP.Application.Auth.UseCases.Logout;

public sealed class LogoutHandler : IRequestHandler<LogoutCommand, Result<string>>
{
    private readonly IRefreshTokenService _refreshTokenService;

    public LogoutHandler(IRefreshTokenService refreshTokenService)
        => _refreshTokenService = refreshTokenService;

    public async Task<Result<string>> Handle(LogoutCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.RawRefreshToken))
            return Result<string>.Failure("Se requiere el refresh token para cerrar sesión.");

        if (command.AllDevices)
        {
            var validation = await _refreshTokenService.ValidateAndRotateAsync(command.RawRefreshToken, ct);
            if (!validation.IsValid)
                return Result<string>.Failure(validation.Error ?? "Refresh token inválido.");

            await _refreshTokenService.RevokeAllForUserAsync(
                validation.UserId, validation.TenantId, "Logout global", ct);

            return Result<string>.Success("Sesión cerrada en todos los dispositivos.");
        }

        await _refreshTokenService.RevokeAsync(command.RawRefreshToken, "Logout", ct);
        return Result<string>.Success("Sesión cerrada correctamente.");
    }

    // Backward-compatible helper used by integration tests.
    public Task<Result<string>> HandleAsync(string rawRefreshToken, bool allDevices, CancellationToken ct = default)
        => Handle(new LogoutCommand(rawRefreshToken, allDevices), ct);
}

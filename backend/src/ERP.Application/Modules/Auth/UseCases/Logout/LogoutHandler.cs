using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;
using MediatR;

namespace ERP.Application.Auth.UseCases.Logout;

/// <summary>
/// Fase E: además de revocar el/los refresh token(s), cierra la(s) UserSession asociada(s) —
/// antes solo se revocaba el token y la sesión quedaba "Active" en el dashboard administrativo
/// hasta expirar por el job de Hangfire. Nunca reimplementa el cierre: reutiliza
/// UserSession.CloseManually, el mismo método de dominio que usa CloseUserSessionAdminHandler.
/// </summary>
public sealed class LogoutHandler : IRequestHandler<LogoutCommand, Result<string>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUserSessionRepository _userSessionRepository;

    public LogoutHandler(
        IRefreshTokenService refreshTokenService,
        IUserSessionRepository userSessionRepository
    )
    {
        _refreshTokenService = refreshTokenService;
        _userSessionRepository = userSessionRepository;
    }

    public async Task<Result<string>> Handle(
        LogoutCommand command,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(command.RawRefreshToken))
            return Result<string>.Failure("Se requiere el refresh token para cerrar sesión.");

        if (command.AllDevices)
        {
            var validation = await _refreshTokenService.ValidateAndRotateAsync(
                command.RawRefreshToken,
                cancellationToken
            );
            if (!validation.IsValid)
                return Result<string>.Failure(validation.Error ?? "Refresh token inválido.");

            await _refreshTokenService.RevokeAllForUserAsync(
                validation.UserId,
                validation.TenantId,
                "Logout global",
                cancellationToken
            );

            var activeSessions = await _userSessionRepository.GetActiveSessionsAsync(
                validation.UserId,
                validation.TenantId,
                cancellationToken
            );
            if (activeSessions.Count > 0)
            {
                foreach (var session in activeSessions)
                    session.CloseManually(validation.UserId);
                await _userSessionRepository.SaveChangesAsync(cancellationToken);
            }

            return Result<string>.Success("Sesión cerrada en todos los dispositivos.");
        }

        var refreshTokenId = await _refreshTokenService.RevokeAsync(
            command.RawRefreshToken,
            "Logout",
            cancellationToken
        );
        if (refreshTokenId is Guid id)
        {
            var session = await _userSessionRepository.GetByRefreshTokenIdAsync(
                id,
                cancellationToken
            );
            if (session is not null)
            {
                session.CloseManually(session.IdentityUserId);
                await _userSessionRepository.SaveChangesAsync(cancellationToken);
            }
        }

        return Result<string>.Success("Sesión cerrada correctamente.");
    }

    // Backward-compatible helper used by integration tests.
    public Task<Result<string>> HandleAsync(
        string rawRefreshToken,
        bool allDevices,
        CancellationToken cancellationToken = default
    ) => Handle(new LogoutCommand(rawRefreshToken, allDevices), cancellationToken);
}

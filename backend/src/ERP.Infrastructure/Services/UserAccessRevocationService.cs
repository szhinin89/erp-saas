using ERP.Application.Common.Interfaces;
using ERP.Domain.Access.Interfaces;

namespace ERP.Infrastructure.Services;

/// <inheritdoc cref="IUserAccessRevocationService"/>
public sealed class UserAccessRevocationService : IUserAccessRevocationService
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IUserSessionRepository _userSessionRepository;

    public UserAccessRevocationService(
        IRefreshTokenService refreshTokenService,
        IUserSessionRepository userSessionRepository)
    {
        _refreshTokenService = refreshTokenService;
        _userSessionRepository = userSessionRepository;
    }

    public async Task RevokeAllAccessAsync(
        Guid userId, Guid tenantId, Guid actorId, string reason, CancellationToken cancellationToken = default)
    {
        await _refreshTokenService.RevokeAllForUserAsync(userId, tenantId, reason, cancellationToken);

        // Cross-company dentro del tenant a propósito: el objetivo es invalidar TODO el acceso
        // del usuario, no solo el de la empresa activa del admin (mismo criterio documentado en
        // IUserSessionRepository.GetActiveSessionsAsync).
        var sessions = await _userSessionRepository.GetActiveSessionsAsync(userId, tenantId, cancellationToken);
        foreach (var session in sessions)
            session.CloseManually(actorId);

        if (sessions.Count > 0)
            await _userSessionRepository.SaveChangesAsync(cancellationToken);
    }
}

using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, Guid subscriberId, CancellationToken ct = default);
    /// <summary>Revoca atómicamente si sigue activo y no expirado (evita doble rotación concurrente).</summary>
    Task<bool> TryRevokeForRotationAsync(string tokenHash, string replacedByHash, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

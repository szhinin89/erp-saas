using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, Guid subscriberId, CancellationToken ct = default);
    /// <summary>Revoca e inserta sucesor en una sola transacción (rotación atómica).</summary>
    Task<(bool Success, RefreshToken? Previous)> TryRotateAsync(
        string tokenHash, RefreshToken successor, CancellationToken ct = default);
    /// <summary>Revoca todos los tokens activos de una familia (replay sospechoso).</summary>
    Task<int> RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

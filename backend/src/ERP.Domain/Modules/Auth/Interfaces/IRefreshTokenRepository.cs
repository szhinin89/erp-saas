using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
    Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    );

    /// <summary>Revoca e inserta sucesor en una sola transacción (rotación atómica).</summary>
    Task<(bool Success, RefreshToken? Previous)> TryRotateAsync(
        string tokenHash,
        RefreshToken successor,
        CancellationToken cancellationToken = default
    );

    /// <summary>Revoca todos los tokens activos de una familia (replay sospechoso).</summary>
    Task<int> RevokeFamilyAsync(
        Guid familyId,
        string reason,
        CancellationToken cancellationToken = default
    );
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using ERP.Domain.Auth.Entities;

namespace ERP.Domain.Auth.Interfaces;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(Guid userId, Guid subscriberId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

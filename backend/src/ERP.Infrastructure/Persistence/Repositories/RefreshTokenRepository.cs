using Microsoft.EntityFrameworkCore;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ErpDbContext _context;

    public RefreshTokenRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => _context.RefreshTokens.AddAsync(token, ct).AsTask();

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
        Guid userId, Guid subscriberId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _context.RefreshTokens
            .Where(t => t.UserId == userId
                     && t.SubscriberId == subscriberId
                     && !t.IsRevoked
                     && t.ExpiresAt > now)
            .ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

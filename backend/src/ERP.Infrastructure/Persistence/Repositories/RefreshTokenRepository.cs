using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RotationLocks = new();

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

    public async Task<bool> TryRevokeForRotationAsync(
        string tokenHash, string replacedByHash, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var providerName = _context.Database.ProviderName ?? string.Empty;

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var affected = await _context.RefreshTokens
                .Where(t => t.TokenHash == tokenHash && !t.IsRevoked && t.ExpiresAt > now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.IsRevoked, true)
                    .SetProperty(t => t.RevokedAt, now)
                    .SetProperty(t => t.ReasonRevoked, "Rotación")
                    .SetProperty(t => t.ReplacedByHash, replacedByHash), ct);

            return affected == 1;
        }

        var gate = RotationLocks.GetOrAdd(tokenHash, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var tracked = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

            if (tracked is null || tracked.IsRevoked || tracked.ExpiresAt <= now)
                return false;

            tracked.Revoke("Rotación", replacedByHash);
            await _context.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

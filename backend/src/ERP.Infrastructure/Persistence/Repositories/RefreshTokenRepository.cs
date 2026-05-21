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

    public async Task<(bool Success, RefreshToken? Previous)> TryRotateAsync(
        string tokenHash, RefreshToken successor, CancellationToken ct = default)
    {
        var providerName = _context.Database.ProviderName ?? string.Empty;
        if (!providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var gate = RotationLocks.GetOrAdd(tokenHash, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                return await RotateInTransactionAsync(tokenHash, successor, ct);
            }
            finally
            {
                gate.Release();
            }
        }

        return await RotateInTransactionAsync(tokenHash, successor, ct);
    }

    private async Task<(bool Success, RefreshToken? Previous)> RotateInTransactionAsync(
        string tokenHash, RefreshToken successor, CancellationToken ct)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.UtcNow;
            var stored = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

            if (stored is null || stored.IsRevoked || stored.ExpiresAt <= now)
            {
                await tx.RollbackAsync(ct);
                return (false, stored);
            }

            stored.Revoke("Rotación", successor.TokenHash);
            await _context.RefreshTokens.AddAsync(successor, ct);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (true, stored);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var tokens = await _context.RefreshTokens
            .Where(t => t.FamilyId == familyId && !t.IsRevoked && t.ExpiresAt > now)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.Revoke(reason);

        if (tokens.Count > 0)
            await _context.SaveChangesAsync(ct);

        return tokens.Count;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);
}

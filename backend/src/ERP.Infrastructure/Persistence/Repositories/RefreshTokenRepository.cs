using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ERP.Infrastructure.Persistence.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RotationLocks = new();

    private readonly ErpDbContext _context;

    public RefreshTokenRepository(ErpDbContext context) => _context = context;

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens.AddAsync(token, cancellationToken).AsTask();

    public Task<RefreshToken?> GetByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default
    ) =>
        _context.RefreshTokens.FirstOrDefaultAsync(
            t => t.TokenHash == tokenHash,
            cancellationToken
        );

    public async Task<IReadOnlyList<RefreshToken>> GetActiveByUserAsync(
        Guid userId,
        Guid tenantId,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTime.UtcNow;
        return await _context
            .RefreshTokens.Where(t =>
                t.UserId == userId && t.TenantId == tenantId && !t.IsRevoked && t.ExpiresAt > now
            )
            .ToListAsync(cancellationToken);
    }

    public async Task<(bool Success, RefreshToken? Previous)> TryRotateAsync(
        string tokenHash,
        RefreshToken successor,
        CancellationToken cancellationToken = default
    )
    {
        var providerName = _context.Database.ProviderName ?? string.Empty;
        if (!providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            var gate = RotationLocks.GetOrAdd(tokenHash, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                return await RotateInTransactionAsync(tokenHash, successor, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        return await RotateInTransactionAsync(tokenHash, successor, cancellationToken);
    }

    private async Task<(bool Success, RefreshToken? Previous)> RotateInTransactionAsync(
        string tokenHash,
        RefreshToken successor,
        CancellationToken cancellationToken
    )
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var now = DateTime.UtcNow;
            var stored = await _context.RefreshTokens.FirstOrDefaultAsync(
                t => t.TokenHash == tokenHash,
                cancellationToken
            );

            if (stored is null || stored.IsRevoked || stored.ExpiresAt <= now)
            {
                await tx.RollbackAsync(cancellationToken);
                return (false, stored);
            }

            stored.Revoke("Rotación", successor.TokenHash);
            await _context.RefreshTokens.AddAsync(successor, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return (true, stored);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> RevokeFamilyAsync(
        Guid familyId,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTime.UtcNow;
        var tokens = await _context
            .RefreshTokens.Where(t => t.FamilyId == familyId && !t.IsRevoked && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
            token.Revoke(reason);

        if (tokens.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return tokens.Count;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}

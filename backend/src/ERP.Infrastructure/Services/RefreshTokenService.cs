using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RotationGates = new();

    private readonly IRefreshTokenRepository _repo;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IRefreshTokenRepository repo,
        ILogger<RefreshTokenService> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task<(string RawToken, DateTime Expiry)> CreateAsync(
        Guid userId, Guid subscriberId, Guid? companyId, string userType, CancellationToken ct = default)
    {
        var rawToken  = GenerateRaw();
        var tokenHash = Hash(rawToken);

        var entity = RefreshToken.Create(userId, subscriberId, companyId, userType, tokenHash);
        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        _logger.LogDebug(
            "RefreshToken creado para usuario {UserId} ({UserType}), expira {Expiry}",
            userId, userType, entity.ExpiresAt);

        return (rawToken, entity.ExpiresAt);
    }

    public async Task<RefreshTokenValidationResult> ValidateAndRotateAsync(
        string rawToken, CancellationToken ct = default)
    {
        var tokenHash = Hash(rawToken);
        var gate = RotationGates.GetOrAdd(tokenHash, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            return await ValidateAndRotateCoreAsync(rawToken, tokenHash, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<RefreshTokenValidationResult> ValidateAndRotateCoreAsync(
        string rawToken, string tokenHash, CancellationToken ct)
    {
        var stored    = await _repo.GetByHashAsync(tokenHash, ct);
        if (stored is null)
            return RefreshTokenValidationResult.Fail("Refresh token no válido.");

        if (stored.IsRevoked)
        {
            // Posible reutilización maliciosa — revocar toda la familia
            _logger.LogWarning(
                "Intento de reutilizar refresh token revocado para usuario {UserId}. " +
                "Revocando todos los tokens activos.", stored.UserId);
            await RevokeAllForUserAsync(stored.UserId, stored.SubscriberId, "Reutilización detectada", ct);
            return RefreshTokenValidationResult.Fail("Refresh token revocado. Inicia sesión nuevamente.");
        }

        if (!stored.IsActive)
            return RefreshTokenValidationResult.Fail("Refresh token expirado. Inicia sesión nuevamente.");

        var newRaw  = GenerateRaw();
        var newHash = Hash(newRaw);
        var revoked = await _repo.TryRevokeForRotationAsync(tokenHash, newHash, ct);

        if (!revoked)
        {
            var again = await _repo.GetByHashAsync(tokenHash, ct);
            if (again is not null && again.IsRevoked)
            {
                _logger.LogWarning(
                    "Intento de reutilizar refresh token revocado para usuario {UserId}. " +
                    "Revocando todos los tokens activos.", stored.UserId);
                await RevokeAllForUserAsync(stored.UserId, stored.SubscriberId, "Reutilización detectada", ct);
                return RefreshTokenValidationResult.Fail("Refresh token revocado. Inicia sesión nuevamente.");
            }

            return RefreshTokenValidationResult.Fail("Refresh token ya utilizado. Inicia sesión nuevamente.");
        }

        var entity = RefreshToken.Create(
            stored.UserId, stored.SubscriberId, stored.CompanyId, stored.UserType, newHash);
        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        return RefreshTokenValidationResult.Ok(
            stored.UserId, stored.SubscriberId, stored.CompanyId, stored.UserType, newRaw, entity.ExpiresAt);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId, Guid subscriberId, string reason, CancellationToken ct = default)
    {
        var tokens = await _repo.GetActiveByUserAsync(userId, subscriberId, ct);
        foreach (var t in tokens)
            t.Revoke(reason);

        if (tokens.Count > 0)
            await _repo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Revocados {Count} refresh tokens de usuario {UserId} — motivo: {Reason}",
            tokens.Count, userId, reason);
    }

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken ct = default)
    {
        var tokenHash = Hash(rawToken);
        var stored    = await _repo.GetByHashAsync(tokenHash, ct);

        if (stored is null || stored.IsRevoked) return;

        stored.Revoke(reason);
        await _repo.SaveChangesAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string GenerateRaw()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string raw)
    {
        var inputBytes = Encoding.UTF8.GetBytes(raw);
        var hashBytes  = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }
}

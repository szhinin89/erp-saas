using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;

namespace ERP.Infrastructure.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RotationGates = new();

    private readonly IRefreshTokenRepository _repo;
    private readonly RefreshTokenRateLimiter _rateLimiter;
    private readonly AuthOptions _authOptions;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IRefreshTokenRepository repo,
        RefreshTokenRateLimiter rateLimiter,
        IOptions<AuthOptions> authOptions,
        ILogger<RefreshTokenService> logger)
    {
        _repo         = repo;
        _rateLimiter  = rateLimiter;
        _authOptions  = authOptions.Value;
        _logger       = logger;
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
            "RefreshToken creado userId={UserId} familyId={FamilyId} userType={UserType} expiry={Expiry}",
            userId, entity.FamilyId, userType, entity.ExpiresAt);

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
        var stored = await _repo.GetByHashAsync(tokenHash, ct);
        if (stored is null)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRotationFailed, null, null, null, "Token no encontrado");
            return RefreshTokenValidationResult.Fail("Refresh token no válido.");
        }

        if (stored.IsRevoked)
            return await HandleRevokedReuseAsync(stored, ct);

        if (!stored.IsActive)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRotationFailed, stored, null, null, "Expirado");
            return RefreshTokenValidationResult.Fail("Refresh token expirado. Inicia sesión nuevamente.");
        }

        if (!await CheckRateLimitsAsync(stored, ct))
            return RefreshTokenValidationResult.RateLimited("Demasiados intentos de renovación. Espera un momento.");

        var newRaw  = GenerateRaw();
        var newHash = Hash(newRaw);
        var successor = RefreshToken.Create(
            stored.UserId,
            stored.SubscriberId,
            stored.CompanyId,
            stored.UserType,
            newHash,
            familyId: stored.FamilyId,
            parentTokenId: stored.Id,
            rotationDepth: stored.RotationDepth + 1);

        var (rotated, previous) = await _repo.TryRotateAsync(tokenHash, successor, ct);

        if (!rotated)
        {
            var again = await _repo.GetByHashAsync(tokenHash, ct);
            if (again is not null && again.IsRevoked)
                return await HandleRevokedReuseAsync(again, ct);

            LogAudit(RefreshTokenAuditEvents.RefreshRotationFailed, stored, null, null, "Rotación concurrente");
            return RefreshTokenValidationResult.Fail("Refresh token ya utilizado. Inicia sesión nuevamente.");
        }

        LogAudit(
            RefreshTokenAuditEvents.RefreshSuccess,
            previous ?? stored,
            successor.Id,
            successor.TokenHash,
            null);

        return RefreshTokenValidationResult.Ok(
            stored.UserId, stored.SubscriberId, stored.CompanyId, stored.UserType, newRaw, successor.ExpiresAt);
    }

    private async Task<RefreshTokenValidationResult> HandleRevokedReuseAsync(
        RefreshToken stored, CancellationToken ct)
    {
        if (IsBenignRotationReuse(stored))
        {
            LogAudit(
                RefreshTokenAuditEvents.RefreshReuseBenign,
                stored,
                null,
                null,
                "Reuso inmediato post-rotación");
            return RefreshTokenValidationResult.Fail("Refresh token ya utilizado. Inicia sesión nuevamente.");
        }

        LogAudit(
            RefreshTokenAuditEvents.RefreshReuseSuspicious,
            stored,
            null,
            null,
            "Reuso tardío — posible robo");

        var revokedCount = await _repo.RevokeFamilyAsync(stored.FamilyId, "Reutilización detectada", ct);

        _logger.LogWarning(
            "{Event} userId={UserId} subscriberId={SubscriberId} familyId={FamilyId} tokenId={TokenId} revokedCount={RevokedCount}",
            RefreshTokenAuditEvents.RefreshFamilyRevoked,
            stored.UserId,
            stored.SubscriberId,
            stored.FamilyId,
            stored.Id,
            revokedCount);

        return RefreshTokenValidationResult.Fail("Refresh token revocado. Inicia sesión nuevamente.");
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
            "Revocados {Count} refresh tokens userId={UserId} reason={Reason}",
            tokens.Count, userId, reason);
    }

    public async Task RevokeFamilyAsync(Guid familyId, string reason, CancellationToken ct = default)
    {
        var count = await _repo.RevokeFamilyAsync(familyId, reason, ct);
        _logger.LogInformation(
            "Familia revocada familyId={FamilyId} count={Count} reason={Reason}",
            familyId, count, reason);
    }

    public async Task RevokeAsync(string rawToken, string reason, CancellationToken ct = default)
    {
        var tokenHash = Hash(rawToken);
        var stored    = await _repo.GetByHashAsync(tokenHash, ct);

        if (stored is null || stored.IsRevoked) return;

        stored.Revoke(reason);
        await _repo.SaveChangesAsync(ct);
    }

    private async Task<bool> CheckRateLimitsAsync(RefreshToken stored, CancellationToken ct)
    {
        var window = TimeSpan.FromMinutes(1);
        var userOk = await _rateLimiter.TryAcquireAsync(
            $"user:{stored.UserId}",
            _authOptions.RefreshRateLimitPerUserPerMinute,
            window,
            ct);
        if (!userOk)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRateLimited, stored, null, null, "user");
            return false;
        }

        var familyOk = await _rateLimiter.TryAcquireAsync(
            $"family:{stored.FamilyId}",
            _authOptions.RefreshRateLimitPerFamilyPerMinute,
            window,
            ct);
        if (!familyOk)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRateLimited, stored, null, null, "family");
            return false;
        }

        return true;
    }

    private bool IsBenignRotationReuse(RefreshToken stored)
        => stored.ReasonRevoked == "Rotación"
           && stored.RevokedAt.HasValue
           && (DateTime.UtcNow - stored.RevokedAt.Value).TotalSeconds
              < _authOptions.RefreshRotationGraceSeconds;

    private void LogAudit(
        string eventName,
        RefreshToken? token,
        Guid? successorId,
        string? successorHash,
        string? detail)
    {
        _logger.LogInformation(
            "{Event} userId={UserId} subscriberId={SubscriberId} familyId={FamilyId} tokenId={TokenId} " +
            "successorId={SuccessorId} successorHash={SuccessorHash} depth={Depth} detail={Detail}",
            eventName,
            token?.UserId,
            token?.SubscriberId,
            token?.FamilyId,
            token?.Id,
            successorId,
            successorHash,
            token?.RotationDepth,
            detail);
    }

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

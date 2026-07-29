using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using ERP.Application.Common.Security;
using ERP.Domain.Auth.Entities;
using ERP.Domain.Auth.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Services;

public sealed partial class RefreshTokenService : IRefreshTokenService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RotationGates = new();

    private readonly IRefreshTokenRepository _repo;
    private readonly RefreshTokenRateLimiter _rateLimiter;
    private readonly AuthOptions _authOptions;
    private readonly ISecurityMetrics _metrics;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(
        IRefreshTokenRepository repo,
        RefreshTokenRateLimiter rateLimiter,
        IOptions<AuthOptions> authOptions,
        ISecurityMetrics metrics,
        ILogger<RefreshTokenService> logger
    )
    {
        _repo = repo;
        _rateLimiter = rateLimiter;
        _authOptions = authOptions.Value;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<(string RawToken, DateTime Expiry)> CreateAsync(
        Guid userId,
        Guid tenantId,
        Guid? companyId,
        string userType,
        CancellationToken cancellationToken = default
    )
    {
        var rawToken = GenerateRaw();
        var tokenHash = Hash(rawToken);

        var entity = RefreshToken.Create(userId, tenantId, companyId, userType, tokenHash);
        await _repo.AddAsync(entity, cancellationToken);
        await _repo.SaveChangesAsync(cancellationToken);

        LogRefreshTokenCreated(userId, entity.FamilyId, userType, entity.ExpiresAt);

        return (rawToken, entity.ExpiresAt);
    }

    public async Task<(RefreshToken Entity, string RawToken)> CreateWithoutSaveAsync(
        Guid userId,
        Guid tenantId,
        Guid? companyId,
        string userType,
        CancellationToken cancellationToken = default
    )
    {
        var rawToken = GenerateRaw();
        var tokenHash = Hash(rawToken);

        var entity = RefreshToken.Create(userId, tenantId, companyId, userType, tokenHash);
        await _repo.AddAsync(entity, cancellationToken);

        LogRefreshTokenCreated(userId, entity.FamilyId, userType, entity.ExpiresAt);

        return (entity, rawToken);
    }

    public async Task<RefreshTokenValidationResult> ValidateAndRotateAsync(
        string rawToken,
        CancellationToken cancellationToken = default
    )
    {
        var tokenHash = Hash(rawToken);
        var gate = RotationGates.GetOrAdd(tokenHash, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            return await ValidateAndRotateCoreAsync(rawToken, tokenHash, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<RefreshTokenValidationResult> ValidateAndRotateCoreAsync(
        string rawToken,
        string tokenHash,
        CancellationToken cancellationToken
    )
    {
        var stored = await _repo.GetByHashAsync(tokenHash, cancellationToken);
        if (stored is null)
        {
            LogAudit(
                RefreshTokenAuditEvents.RefreshRotationFailed,
                null,
                null,
                null,
                "Token no encontrado"
            );
            _metrics.RecordJwtRefreshRevoked(RefreshTags(null, null, "invalid_token"));
            return RefreshTokenValidationResult.Fail("Refresh token no válido.");
        }

        if (stored.IsRevoked)
            return await HandleRevokedReuseAsync(stored, cancellationToken);

        if (!stored.IsActive)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRotationFailed, stored, null, null, "Expirado");
            return RefreshTokenValidationResult.Fail(
                "Refresh token expirado. Inicia sesión nuevamente."
            );
        }

        if (!await CheckRateLimitsAsync(stored, cancellationToken))
            return RefreshTokenValidationResult.RateLimited(
                "Demasiados intentos de renovación. Espera un momento."
            );

        var newRaw = GenerateRaw();
        var newHash = Hash(newRaw);
        var successor = RefreshToken.Create(
            stored.UserId,
            stored.TenantId,
            stored.CompanyId,
            stored.UserType,
            newHash,
            familyId: stored.FamilyId,
            parentTokenId: stored.Id,
            rotationDepth: stored.RotationDepth + 1
        );

        var (rotated, previous) = await _repo.TryRotateAsync(
            tokenHash,
            successor,
            cancellationToken
        );

        if (!rotated)
        {
            var again = await _repo.GetByHashAsync(tokenHash, cancellationToken);
            if (again is not null && again.IsRevoked)
                return await HandleRevokedReuseAsync(again, cancellationToken);

            LogAudit(
                RefreshTokenAuditEvents.RefreshRotationFailed,
                stored,
                null,
                null,
                "Rotación concurrente"
            );
            return RefreshTokenValidationResult.Fail(
                "Refresh token ya utilizado. Inicia sesión nuevamente."
            );
        }

        LogAudit(
            RefreshTokenAuditEvents.RefreshSuccess,
            previous ?? stored,
            successor.Id,
            successor.TokenHash,
            null
        );

        return RefreshTokenValidationResult.Ok(
            stored.UserId,
            stored.TenantId,
            stored.CompanyId,
            stored.UserType,
            newRaw,
            successor.ExpiresAt
        );
    }

    private async Task<RefreshTokenValidationResult> HandleRevokedReuseAsync(
        RefreshToken stored,
        CancellationToken cancellationToken
    )
    {
        if (IsBenignRotationReuse(stored))
        {
            LogAudit(
                RefreshTokenAuditEvents.RefreshReuseBenign,
                stored,
                null,
                null,
                "Reuso inmediato post-rotación"
            );
            return RefreshTokenValidationResult.Fail(
                "Refresh token ya utilizado. Inicia sesión nuevamente."
            );
        }

        LogAudit(
            RefreshTokenAuditEvents.RefreshReuseSuspicious,
            stored,
            null,
            null,
            "Reuso tardío — posible robo"
        );

        var revokedCount = await _repo.RevokeFamilyAsync(
            stored.FamilyId,
            "Reutilización detectada",
            cancellationToken
        );

        _metrics.RecordJwtRefreshRevoked(
            RefreshTags(stored.TenantId, stored.CompanyId, "token_reuse_family_revoked")
        );

        LogFamilyRevoked(
            RefreshTokenAuditEvents.RefreshFamilyRevoked,
            stored.UserId,
            stored.TenantId,
            stored.FamilyId,
            stored.Id,
            revokedCount
        );

        return RefreshTokenValidationResult.Fail(
            "Refresh token revocado. Inicia sesión nuevamente."
        );
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var tokens = await _repo.GetActiveByUserAsync(userId, tenantId, cancellationToken);
        foreach (var t in tokens)
            t.Revoke(reason);

        if (tokens.Count > 0)
            await _repo.SaveChangesAsync(cancellationToken);

        LogTokensRevoked(tokens.Count, userId, reason);
    }

    public async Task RevokeFamilyAsync(
        Guid familyId,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var count = await _repo.RevokeFamilyAsync(familyId, reason, cancellationToken);
        LogFamilyRevokedByFamily(familyId, count, reason);
    }

    public async Task<Guid?> RevokeAsync(
        string rawToken,
        string reason,
        CancellationToken cancellationToken = default
    )
    {
        var tokenHash = Hash(rawToken);
        var stored = await _repo.GetByHashAsync(tokenHash, cancellationToken);

        if (stored is null)
            return null;

        if (!stored.IsRevoked)
        {
            stored.Revoke(reason);
            await _repo.SaveChangesAsync(cancellationToken);

            _metrics.RecordJwtRefreshRevoked(
                RefreshTags(stored.TenantId, stored.CompanyId, "explicit_revoke")
            );
        }

        return stored.Id;
    }

    private static SecurityMetricTags RefreshTags(
        Guid? tenantId,
        Guid? companyId,
        string requestType
    ) =>
        new(
            TenantId: tenantId,
            CompanyId: companyId,
            Endpoint: "/api/v1/auth/refresh",
            RequestType: requestType
        );

    private async Task<bool> CheckRateLimitsAsync(
        RefreshToken stored,
        CancellationToken cancellationToken
    )
    {
        var window = TimeSpan.FromMinutes(1);
        var userOk = await _rateLimiter.TryAcquireAsync(
            $"user:{stored.UserId}",
            _authOptions.RefreshRateLimitPerUserPerMinute,
            window,
            cancellationToken
        );
        if (!userOk)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRateLimited, stored, null, null, "user");
            return false;
        }

        var familyOk = await _rateLimiter.TryAcquireAsync(
            $"family:{stored.FamilyId}",
            _authOptions.RefreshRateLimitPerFamilyPerMinute,
            window,
            cancellationToken
        );
        if (!familyOk)
        {
            LogAudit(RefreshTokenAuditEvents.RefreshRateLimited, stored, null, null, "family");
            return false;
        }

        return true;
    }

    private bool IsBenignRotationReuse(RefreshToken stored) =>
        stored.ReasonRevoked == "Rotación"
        && stored.RevokedAt.HasValue
        && (DateTime.UtcNow - stored.RevokedAt.Value).TotalSeconds
            < _authOptions.RefreshRotationGraceSeconds;

    private void LogAudit(
        string eventName,
        RefreshToken? token,
        Guid? successorId,
        string? successorHash,
        string? detail
    )
    {
        LogAuditEvent(
            eventName,
            token?.UserId,
            token?.TenantId,
            token?.FamilyId,
            token?.Id,
            successorId,
            successorHash,
            token?.RotationDepth,
            detail
        );
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
        var hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "RefreshToken creado userId={UserId} familyId={FamilyId} userType={UserType} expiry={Expiry}"
    )]
    private partial void LogRefreshTokenCreated(
        Guid userId,
        Guid familyId,
        string userType,
        DateTime expiry
    );

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Event} userId={UserId} tenantId={TenantId} familyId={FamilyId} tokenId={TokenId} revokedCount={RevokedCount}"
    )]
    private partial void LogFamilyRevoked(
        string @event,
        Guid userId,
        Guid tenantId,
        Guid familyId,
        Guid tokenId,
        int revokedCount
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Revocados {Count} refresh tokens userId={UserId} reason={Reason}"
    )]
    private partial void LogTokensRevoked(int count, Guid userId, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Familia revocada familyId={FamilyId} count={Count} reason={Reason}"
    )]
    private partial void LogFamilyRevokedByFamily(Guid familyId, int count, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "{Event} userId={UserId} tenantId={TenantId} familyId={FamilyId} tokenId={TokenId} "
            + "successorId={SuccessorId} successorHash={SuccessorHash} depth={Depth} detail={Detail}"
    )]
    private partial void LogAuditEvent(
        string @event,
        Guid? userId,
        Guid? tenantId,
        Guid? familyId,
        Guid? tokenId,
        Guid? successorId,
        string? successorHash,
        int? depth,
        string? detail
    );
}

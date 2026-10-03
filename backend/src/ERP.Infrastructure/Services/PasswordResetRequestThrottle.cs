using ERP.Application.Common.Config;
using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Security.Cryptography;
using System.Text;

namespace ERP.Infrastructure.Services;

/// <summary>
/// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — cupo de <c>forgot-password</c> por email
/// normalizado, sobre la misma caché distribuida que el rate limit de refresh. La clave usa el
/// SHA-256 del email (nunca el email en claro).
/// </summary>
public sealed class PasswordResetRequestThrottle : IPasswordResetRequestThrottle
{
    private const string KeyPrefix = "erp:auth:pwreset:rl:";

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly IOptions<PasswordResetOptions> _options;

    /// <param name="redis">Conexión Redis compartida si está configurada; sin ella, fallback en memoria.</param>
    public PasswordResetRequestThrottle(
        IDistributedCache cache,
        IOptions<PasswordResetOptions> options,
        IConnectionMultiplexer? redis = null
    )
    {
        _cache = cache;
        _options = options;
        _redis = redis;
    }

    public Task<bool> TryAcquireAsync(string normalizedEmail, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        return DistributedFixedWindowRateLimit.TryAcquireAsync(
            _cache,
            _redis,
            KeyPrefix + Fingerprint(normalizedEmail),
            Math.Max(1, options.IdentityRequestLimit),
            TimeSpan.FromMinutes(Math.Max(1, options.IdentityRequestWindowMinutes)),
            cancellationToken
        );
    }

    private static string Fingerprint(string normalizedEmail) =>
        Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail.Trim().ToLowerInvariant()))
        );
}

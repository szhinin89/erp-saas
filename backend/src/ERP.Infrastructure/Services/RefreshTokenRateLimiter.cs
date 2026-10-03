using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ERP.Infrastructure.Services;

/// <summary>Rate limit distribuido simple para rotaciones refresh (usuario / familia).</summary>
public sealed class RefreshTokenRateLimiter
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RefreshTokenRateLimiter> _logger;

    /// <param name="redis">Conexión Redis compartida si está configurada; sin ella, fallback en memoria.</param>
    public RefreshTokenRateLimiter(
        IDistributedCache cache,
        ILogger<RefreshTokenRateLimiter> logger,
        IConnectionMultiplexer? redis = null
    )
    {
        _cache = cache;
        _logger = logger;
        _redis = redis;
    }

    public Task<bool> TryAcquireAsync(
        string partitionKey,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default
    ) =>
        DistributedFixedWindowRateLimit.TryAcquireAsync(
            _cache,
            _redis,
            $"erp:refresh:rl:{partitionKey}",
            limit,
            window,
            cancellationToken
        );
}

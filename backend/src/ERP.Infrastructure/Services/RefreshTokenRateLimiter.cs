using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Services;

/// <summary>Rate limit distribuido simple para rotaciones refresh (usuario / familia).</summary>
public sealed class RefreshTokenRateLimiter
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RefreshTokenRateLimiter> _logger;

    public RefreshTokenRateLimiter(
        IDistributedCache cache,
        ILogger<RefreshTokenRateLimiter> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(
        string partitionKey, int limit, TimeSpan window, CancellationToken ct = default)
    {
        var key = $"erp:refresh:rl:{partitionKey}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = (long)window.TotalMilliseconds;

        var raw = await _cache.GetStringAsync(key, ct);
        RefreshRateBucket bucket;
        if (string.IsNullOrEmpty(raw))
        {
            bucket = new RefreshRateBucket(now, 1);
        }
        else
        {
            var parts = raw.Split(':');
            var windowStart = long.Parse(parts[0]);
            var count = int.Parse(parts[1]);

            if (now - windowStart >= windowMs)
                bucket = new RefreshRateBucket(now, 1);
            else if (count >= limit)
                return false;
            else
                bucket = new RefreshRateBucket(windowStart, count + 1);
        }

        await _cache.SetStringAsync(
            key,
            $"{bucket.WindowStartMs}:{bucket.Count}",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = window },
            ct);

        return true;
    }

    private readonly record struct RefreshRateBucket(long WindowStartMs, int Count);
}

using System.Text.Json;
using ERP.Application.Subscriptions.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Subscriptions.Caching;

public sealed class DistributedSubscriberEntitlementsSnapshotCache :
    ISubscriberEntitlementsSnapshotCache,
    ISubscriberEntitlementsCacheInvalidator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly SubscriberEntitlementsCacheOptions _options;

    public DistributedSubscriberEntitlementsSnapshotCache(
        IDistributedCache cache,
        IOptions<SubscriberEntitlementsCacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<CachedSubscriberEntitlements?> GetAsync(Guid subscriberId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        var version = await GetCurrentVersionAsync(subscriberId, ct);
        if (version <= 0)
            return null;

        var bytes = await _cache.GetAsync(SnapshotKey(subscriberId, version), ct);
        if (bytes is null || bytes.Length == 0)
            return null;

        return JsonSerializer.Deserialize<CachedSubscriberEntitlements>(bytes, JsonOptions);
    }

    public async Task SetAsync(CachedSubscriberEntitlements cached, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return;

        var subscriberId = cached.Snapshot.SubscriberId ?? Guid.Empty;
        var version = cached.Version > 0 ? cached.Version : 1L;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cached, JsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(Math.Max(30, _options.TtlSeconds)),
        };

        // VersionKey must exist or GetAsync always misses (version <= 0).
        await _cache.SetAsync(
            VersionKey(subscriberId),
            BitConverter.GetBytes(version),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            ct);

        await _cache.SetAsync(SnapshotKey(subscriberId, version), bytes, options, ct);
    }

    public async Task<long> GetCurrentVersionAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(VersionKey(subscriberId), ct);
        if (bytes is null || bytes.Length == 0)
            return 0;
        return BitConverter.ToInt64(bytes, 0);
    }

    public async Task InvalidateAsync(Guid subscriberId, CancellationToken ct = default)
    {
        var next = await GetCurrentVersionAsync(subscriberId, ct) + 1;
        await _cache.SetAsync(
            VersionKey(subscriberId),
            BitConverter.GetBytes(next),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30) },
            ct);
    }

    private static string SnapshotKey(Guid subscriberId, long version)
        => $"entitlements:snapshot:{subscriberId:N}:v{version}";

    private static string VersionKey(Guid subscriberId)
        => $"entitlements:version:{subscriberId:N}";
}

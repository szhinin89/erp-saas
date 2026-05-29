using System.Text.Json;
using ERP.Application.Common.Subscriptions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.SaaS;

/// <summary>
/// Two-level cache: L1 = IMemoryCache (in-process, ~0ms), L2 = IDistributedCache (Redis, ~1ms).
///
/// FAULT TOLERANCE: Any Redis failure falls through to IMemoryCache.
/// A MemoryCache failure falls through to a cache miss (DB rebuild).
/// NEVER blocks the middleware or throws to callers.
///
/// CACHE VERSIONING (FASE 11):
///   Version key: subscription:access:ver:{subscriberId}
///   Snapshot key: subscription:access:{subscriberId}:{version}
///   Invalidation: increment version → old snapshot orphaned, new miss triggers rebuild.
/// </summary>
public sealed class HybridSubscriptionAccessCache : ISubscriptionAccessCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache                _memory;
    private readonly IDistributedCache           _distributed;
    private readonly SubscriptionBillingOptions  _opts;
    private readonly ILogger<HybridSubscriptionAccessCache> _log;

    public HybridSubscriptionAccessCache(
        IMemoryCache memory,
        IDistributedCache distributed,
        IOptions<SubscriptionBillingOptions> options,
        ILogger<HybridSubscriptionAccessCache> log)
    {
        _memory      = memory;
        _distributed = distributed;
        _opts        = options.Value;
        _log         = log;
    }

    // ── ISubscriptionAccessCache ──────────────────────────────────────────────

    public async Task<SubscriptionAccessSnapshot?> TryGetAsync(
        Guid subscriberId, CancellationToken ct = default)
    {
        try
        {
            var version = await GetVersionAsync(subscriberId, ct);
            if (version <= 0) return null;

            var l1Key = L1Key(subscriberId, version);

            // L1: memory
            if (_memory.TryGetValue<SubscriptionAccessSnapshot>(l1Key, out var l1))
                return l1;

            // L2: Redis
            var l2Key  = L2Key(subscriberId, version);
            byte[]? bytes = null;
            try   { bytes = await _distributed.GetAsync(l2Key, ct); }
            catch (Exception ex) { _log.LogWarning(ex, "subscription-access-cache: Redis GET failed, using memory-only"); }

            if (bytes is null || bytes.Length == 0)
                return null;

            var snapshot = JsonSerializer.Deserialize<SubscriptionAccessSnapshot>(bytes, Json);
            if (snapshot is null) return null;

            // Backfill L1
            _memory.Set(l1Key, snapshot, _opts.L1Ttl);
            return snapshot;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "subscription-access-cache: TryGet failed for {SubscriberId}", subscriberId);
            return null;
        }
    }

    public async Task SetAsync(SubscriptionAccessSnapshot snapshot, CancellationToken ct = default)
    {
        try
        {
            var version = snapshot.Version > 0 ? snapshot.Version : 1L;
            var l1Key   = L1Key(snapshot.SubscriberId, version);
            var l2Key   = L2Key(snapshot.SubscriberId, version);
            var verKey  = VersionKey(snapshot.SubscriberId);
            var bytes   = JsonSerializer.SerializeToUtf8Bytes(snapshot, Json);

            // L1
            _memory.Set(l1Key, snapshot, _opts.L1Ttl);

            // L2 version key (long TTL so it outlives the snapshot itself)
            var verBytes = BitConverter.GetBytes(version);
            try
            {
                await _distributed.SetAsync(verKey, verBytes,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _opts.VersionTtl }, ct);

                await _distributed.SetAsync(l2Key, bytes,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _opts.L2Ttl }, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "subscription-access-cache: Redis SET failed for {SubscriberId} — L1 only", snapshot.SubscriberId);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "subscription-access-cache: SetAsync failed for {SubscriberId}", snapshot.SubscriberId);
        }
    }

    public async Task InvalidateAsync(Guid subscriberId, CancellationToken ct = default)
    {
        try
        {
            // Increment version — old snapshot keys become unreachable (orphaned with TTL).
            var newVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var verKey     = VersionKey(subscriberId);

            // Clear L1 pattern: remove all versions by tracking them is complex.
            // Instead, use a sentinel L1 version key.
            _memory.Set(verKey, newVersion, _opts.L1Ttl);

            var verBytes = BitConverter.GetBytes(newVersion);
            try
            {
                await _distributed.SetAsync(verKey, verBytes,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _opts.VersionTtl }, ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "subscription-access-cache: Redis version invalidation failed for {SubscriberId}", subscriberId);
            }

            _log.LogDebug("subscription-access-cache: invalidated {SubscriberId} → version {Version}", subscriberId, newVersion);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "subscription-access-cache: InvalidateAsync failed for {SubscriberId}", subscriberId);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<long> GetVersionAsync(Guid subscriberId, CancellationToken ct)
    {
        var verKey = VersionKey(subscriberId);

        // L1 version
        if (_memory.TryGetValue<long>(verKey, out var l1Ver) && l1Ver > 0)
            return l1Ver;

        // L2 version
        byte[]? bytes = null;
        try   { bytes = await _distributed.GetAsync(verKey, ct); }
        catch (Exception ex) { _log.LogWarning(ex, "subscription-access-cache: Redis version GET failed for {SubscriberId}", subscriberId); }

        if (bytes is null || bytes.Length < 8) return 0L;

        var version = BitConverter.ToInt64(bytes);
        if (version > 0) _memory.Set(verKey, version, _opts.L1Ttl);
        return version;
    }

    // ── Key helpers ───────────────────────────────────────────────────────────

    private static string VersionKey(Guid id)          => $"subscription:access:ver:{id}";
    private static string L1Key(Guid id, long version) => $"subscription:access:{id}:{version}";
    private static string L2Key(Guid id, long version) => $"subscription:access:{id}:{version}";
}

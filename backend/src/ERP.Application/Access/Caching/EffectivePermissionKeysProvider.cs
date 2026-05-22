using System.Collections.Concurrent;
using ERP.Application.Common;
using ERP.Application.Subscriptions;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.Caching;

public sealed class EffectivePermissionKeysProvider : IEffectivePermissionKeysProvider
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> StampedeLocks = new();
    private static readonly TimeSpan StampedeLockWait = TimeSpan.FromMilliseconds(500);

    private readonly IPermissionsCacheService _cache;
    private readonly IPermissionsCacheDiagnostics _diagnostics;
    private readonly IAccessRepository _repo;
    private readonly ISubscriberEntitlementsService _entitlements;

    public EffectivePermissionKeysProvider(
        IPermissionsCacheService cache,
        IPermissionsCacheDiagnostics diagnostics,
        IAccessRepository repo,
        ISubscriberEntitlementsService entitlements)
    {
        _cache = cache;
        _diagnostics = diagnostics;
        _repo = repo;
        _entitlements = entitlements;
    }

    public async Task<IReadOnlyList<string>> GetAllowedKeysAsync(
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        CancellationToken ct = default)
    {
        var cached = await _cache.ReadAsync(subscriberId, companyId, userId, ct);
        if (cached.Keys is not null)
        {
            _diagnostics.RecordHit();
            return cached.Keys;
        }

        var sem = StampedeLocks.GetOrAdd(StampedeLockKey(companyId, userId), _ => new SemaphoreSlim(1, 1));
        var acquired = await sem.WaitAsync(StampedeLockWait, ct);
        if (!acquired)
        {
            _diagnostics.RecordMiss(PermissionsCacheMissReason.StampedeFallback);
            return await LoadFromDatabaseAsync(subscriberId, profileId, ct);
        }

        try
        {
            cached = await _cache.ReadAsync(subscriberId, companyId, userId, ct);
            if (cached.Keys is not null)
            {
                _diagnostics.RecordHit();
                return cached.Keys;
            }

            _diagnostics.RecordMiss(cached.MissReason ?? PermissionsCacheMissReason.NotFound);

            var allowed = await LoadFromDatabaseAsync(subscriberId, profileId, ct);
            await _cache.WriteAsync(subscriberId, companyId, userId, allowed, ttl: null, ct);
            _diagnostics.RecordSet();
            return allowed;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<IReadOnlyList<string>> LoadFromDatabaseAsync(
        Guid subscriberId,
        Guid profileId,
        CancellationToken ct)
    {
        var items = await _repo.GetProfilePermissionsAsync(subscriberId, profileId, ct);
        var candidateKeys = items
            .Where(p => p.IsAllowed)
            .Select(p => p.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var allowed = new List<string>();
        foreach (var key in candidateKeys)
        {
            if (await SubscriberSubscriptionCatalog.TenantAllowsPermissionAsync(
                    subscriberId, _entitlements, key, ct))
                allowed.Add(key);
        }

        allowed.Sort(StringComparer.OrdinalIgnoreCase);
        return allowed;
    }

    private static string StampedeLockKey(Guid companyId, Guid userId)
        => $"{companyId:N}:{userId:N}";
}

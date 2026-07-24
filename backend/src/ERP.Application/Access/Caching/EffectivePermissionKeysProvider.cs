using System.Collections.Concurrent;
using ERP.Application.Common;
using ERP.Domain.Access.Interfaces;

namespace ERP.Application.Access.Caching;

public sealed class EffectivePermissionKeysProvider : IEffectivePermissionKeysProvider
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> StampedeLocks = new();
    private static readonly TimeSpan StampedeLockWait = TimeSpan.FromMilliseconds(500);

    private readonly IPermissionsCacheService _cache;
    private readonly IPermissionsCacheDiagnostics _diagnostics;
    private readonly IAccessRepository _repo;

    public EffectivePermissionKeysProvider(
        IPermissionsCacheService cache,
        IPermissionsCacheDiagnostics diagnostics,
        IAccessRepository repo)
    {
        _cache       = cache;
        _diagnostics = diagnostics;
        _repo        = repo;
    }

    public async Task<IReadOnlyList<string>> GetAllowedKeysAsync(
        Guid tenantId, Guid companyId, Guid userId, Guid profileId,
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.ReadAsync(tenantId, companyId, userId, cancellationToken);
        if (cached.Keys is not null)
        {
            _diagnostics.RecordHit();
            return cached.Keys;
        }

        var sem      = StampedeLocks.GetOrAdd(StampedeLockKey(companyId, userId), _ => new SemaphoreSlim(1, 1));
        var acquired = await sem.WaitAsync(StampedeLockWait, cancellationToken);
        if (!acquired)
        {
            _diagnostics.RecordMiss(PermissionsCacheMissReason.StampedeFallback);
            return await LoadFromDatabaseAsync(tenantId, profileId, cancellationToken);
        }

        try
        {
            cached = await _cache.ReadAsync(tenantId, companyId, userId, cancellationToken);
            if (cached.Keys is not null)
            {
                _diagnostics.RecordHit();
                return cached.Keys;
            }

            _diagnostics.RecordMiss(cached.MissReason ?? PermissionsCacheMissReason.NotFound);

            var allowed = await LoadFromDatabaseAsync(tenantId, profileId, cancellationToken);
            await _cache.WriteAsync(tenantId, companyId, userId, allowed, ttl: null, cancellationToken);
            _diagnostics.RecordSet();
            return allowed;
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<IReadOnlyList<string>> LoadFromDatabaseAsync(
        Guid tenantId, Guid profileId, CancellationToken cancellationToken)
    {
        var items = await _repo.GetProfilePermissionsAsync(tenantId, profileId, cancellationToken);
        var allowed = items
            .Where(p => p.IsAllowed)
            .Select(p => p.PermissionKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return allowed;
    }

    private static string StampedeLockKey(Guid companyId, Guid userId)
        => $"{companyId:N}:{userId:N}";
}

using System.Text.Json;
using ERP.Application.Access.Caching;
using ERP.Application.Subscriptions.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Access.Caching;

public sealed class DistributedPermissionsCacheService : IPermissionsCacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDistributedCache _cache;
    private readonly SaasEntitlementsCacheOptions _options;
    private readonly SemaphoreSlim _stampede = new(1, 1);

    public DistributedPermissionsCacheService(
        IDistributedCache cache,
        IOptions<SaasEntitlementsCacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>?> GetPermissionKeysAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return null;

        var bytes = await _cache.GetAsync(Key(companyId, userId), ct);
        if (bytes is null || bytes.Length == 0)
            return null;

        return JsonSerializer.Deserialize<List<string>>(bytes, JsonOptions);
    }

    public async Task SetPermissionKeysAsync(
        Guid companyId,
        Guid userId,
        IReadOnlyList<string> keys,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return;

        var bytes = JsonSerializer.SerializeToUtf8Bytes(keys, JsonOptions);
        await _cache.SetAsync(
            Key(companyId, userId),
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(Math.Max(60, _options.TtlSeconds)),
            },
            ct);
    }

    public Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken ct = default)
        => _cache.RemoveAsync(Key(companyId, userId), ct);

    public async Task InvalidateCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        await _stampede.WaitAsync(ct);
        try
        {
            await _cache.SetAsync(
                CompanyVersionKey(companyId),
                BitConverter.GetBytes(DateTime.UtcNow.Ticks),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7) },
                ct);
        }
        finally
        {
            _stampede.Release();
        }
    }

    private static string Key(Guid companyId, Guid userId)
        => $"permissions:{companyId:N}:{userId:N}";

    private static string CompanyVersionKey(Guid companyId)
        => $"permissions:version:{companyId:N}";
}

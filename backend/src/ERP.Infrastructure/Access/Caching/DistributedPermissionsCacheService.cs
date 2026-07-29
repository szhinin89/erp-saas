using ERP.Application.Access.Caching;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ERP.Infrastructure.Access.Caching;

public sealed class DistributedPermissionsCacheService : IPermissionsCacheBackend
{
    private const int DefaultTtlSeconds = 300;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions VersionEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
    };

    private readonly IDistributedCache _cache;

    public DistributedPermissionsCacheService(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<PermissionsCacheReadResult> ReadAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var bytes = await _cache.GetAsync(DataKey(companyId, userId), cancellationToken);
        if (bytes is null || bytes.Length == 0)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.NotFound);

        var envelope = JsonSerializer.Deserialize<PermissionCacheEnvelope>(bytes, JsonOptions);
        if (envelope?.Keys is null)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.NotFound);

        var companyVersion = await ReadVersionAsync(CompanyVersionKey(companyId), cancellationToken);
        var tenantVersion = await ReadVersionAsync(TenantVersionKey(tenantId), cancellationToken);

        if (envelope.CompanyVersion != companyVersion || envelope.TenantVersion != tenantVersion)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.VersionMismatch);

        return new PermissionsCacheReadResult(envelope.Keys, null);
    }

    public async Task WriteAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        IReadOnlyList<string> keys,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        var companyVersion = await ReadVersionAsync(CompanyVersionKey(companyId), cancellationToken);
        var tenantVersion = await ReadVersionAsync(TenantVersionKey(tenantId), cancellationToken);

        var envelope = new PermissionCacheEnvelope(
            companyVersion,
            tenantVersion,
            keys.ToList());

        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        await _cache.SetAsync(
            DataKey(companyId, userId),
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(DefaultTtlSeconds),
            },
            cancellationToken);
    }

    public Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
        => _cache.RemoveAsync(DataKey(companyId, userId), cancellationToken);

    public Task BumpCompanyVersionAsync(Guid companyId, CancellationToken cancellationToken = default)
        => BumpVersionAsync(CompanyVersionKey(companyId), cancellationToken);

    public Task BumpTenantVersionAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => BumpVersionAsync(TenantVersionKey(tenantId), cancellationToken);

    private async Task BumpVersionAsync(string versionKey, CancellationToken cancellationToken)
    {
        var next = await ReadVersionAsync(versionKey, cancellationToken) + 1;
        await _cache.SetAsync(versionKey, BitConverter.GetBytes(next), VersionEntryOptions, cancellationToken);
    }

    private async Task<long> ReadVersionAsync(string versionKey, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(versionKey, cancellationToken);
        if (bytes is null || bytes.Length < sizeof(long))
            return 0;

        return BitConverter.ToInt64(bytes, 0);
    }

    private static string DataKey(Guid companyId, Guid userId)
        => $"permissions:{companyId:N}:{userId:N}";

    private static string CompanyVersionKey(Guid companyId)
        => $"permissions:version:{companyId:N}";

    private static string TenantVersionKey(Guid tenantId)
        => $"permissions:version:tenant:{tenantId:N}";

    private sealed record PermissionCacheEnvelope(
        long CompanyVersion,
        long TenantVersion,
        List<string> Keys);
}

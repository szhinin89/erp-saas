using System.Text.Json;
using ERP.Application.Access.Caching;
using ERP.Application.Subscriptions.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Access.Caching;

public sealed class DistributedPermissionsCacheService : IPermissionsCacheBackend
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly DistributedCacheEntryOptions VersionEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(30),
    };

    private readonly IDistributedCache _cache;
    private readonly SaasEntitlementsCacheOptions _options;

    public DistributedPermissionsCacheService(
        IDistributedCache cache,
        IOptions<SaasEntitlementsCacheOptions> options)
    {
        _cache = cache;
        _options = options.Value;
    }

    public async Task<PermissionsCacheReadResult> ReadAsync(
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.Disabled);

        var bytes = await _cache.GetAsync(DataKey(companyId, userId), ct);
        if (bytes is null || bytes.Length == 0)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.NotFound);

        var envelope = JsonSerializer.Deserialize<PermissionCacheEnvelope>(bytes, JsonOptions);
        if (envelope?.Keys is null)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.NotFound);

        var companyVersion = await ReadVersionAsync(CompanyVersionKey(companyId), ct);
        var subscriberVersion = await ReadVersionAsync(SubscriberVersionKey(subscriberId), ct);

        if (envelope.CompanyVersion != companyVersion || envelope.SubscriberVersion != subscriberVersion)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.VersionMismatch);

        return new PermissionsCacheReadResult(envelope.Keys, null);
    }

    public async Task WriteAsync(
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        IReadOnlyList<string> keys,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return;

        var companyVersion = await ReadVersionAsync(CompanyVersionKey(companyId), ct);
        var subscriberVersion = await ReadVersionAsync(SubscriberVersionKey(subscriberId), ct);

        var envelope = new PermissionCacheEnvelope(
            companyVersion,
            subscriberVersion,
            keys.ToList());

        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        await _cache.SetAsync(
            DataKey(companyId, userId),
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromSeconds(Math.Max(60, _options.TtlSeconds)),
            },
            ct);
    }

    public Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return Task.CompletedTask;

        return _cache.RemoveAsync(DataKey(companyId, userId), ct);
    }

    public Task BumpCompanyVersionAsync(Guid companyId, CancellationToken ct = default)
        => BumpVersionAsync(CompanyVersionKey(companyId), ct);

    public Task BumpSubscriberVersionAsync(Guid subscriberId, CancellationToken ct = default)
        => BumpVersionAsync(SubscriberVersionKey(subscriberId), ct);

    private async Task BumpVersionAsync(string versionKey, CancellationToken ct)
    {
        if (!_options.Enabled)
            return;

        var next = await ReadVersionAsync(versionKey, ct) + 1;
        await _cache.SetAsync(versionKey, BitConverter.GetBytes(next), VersionEntryOptions, ct);
    }

    private async Task<long> ReadVersionAsync(string versionKey, CancellationToken ct)
    {
        var bytes = await _cache.GetAsync(versionKey, ct);
        if (bytes is null || bytes.Length < sizeof(long))
            return 0;

        return BitConverter.ToInt64(bytes, 0);
    }

    private static string DataKey(Guid companyId, Guid userId)
        => $"permissions:{companyId:N}:{userId:N}";

    private static string CompanyVersionKey(Guid companyId)
        => $"permissions:version:{companyId:N}";

    private static string SubscriberVersionKey(Guid subscriberId)
        => $"permissions:version:subscriber:{subscriberId:N}";

    private sealed record PermissionCacheEnvelope(
        long CompanyVersion,
        long SubscriberVersion,
        List<string> Keys);
}

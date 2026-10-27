using System.Diagnostics;
using System.Text.Json;
using ERP.Application.Access.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// Entradas en <see cref="IDistributedCache"/>; versiones y marcas en
/// <see cref="PermissionsCacheVersionStore"/> (atómico). SEC-06: una entrada solo es válida si su
/// perfil coincide con el de la membership vigente y sus versiones (tenant, empresa, usuario) son
/// las actuales, sin invalidación en curso; se publica con las versiones observadas antes de cargar
/// la BD (<see cref="PermissionsCacheStamp"/>), nunca con las vigentes al escribir.
/// </summary>
public sealed class DistributedPermissionsCacheService : IPermissionsCacheBackend
{
    public const int DefaultTtlSeconds = 300;

    /// <summary>
    /// Antigüedad máxima de un sello para publicar: acota cuánto después de una invalidación puede
    /// llegar una escritura tardía (ver <see cref="PermissionsCacheVersionStore.PendingExpiry"/>).
    /// </summary>
    public static readonly TimeSpan MaxStampAge = TimeSpan.FromSeconds(60);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly PermissionsCacheVersionStore _versions;

    public DistributedPermissionsCacheService(
        IDistributedCache cache,
        PermissionsCacheVersionStore versions
    )
    {
        _cache = cache;
        _versions = versions;
    }

    public async Task<PermissionsCacheReadResult> ReadAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        CancellationToken cancellationToken = default
    )
    {
        var (pending, versions) = await _versions.ReadAsync(ScopeKeys(tenantId, companyId, userId));
        if (pending)
            return new PermissionsCacheReadResult(
                null,
                PermissionsCacheMissReason.InvalidationPending
            );

        var stamp = new PermissionsCacheStamp(
            versions[0],
            versions[1],
            versions[2],
            Stopwatch.GetTimestamp()
        );

        var bytes = await _cache.GetAsync(DataKey(companyId, userId), cancellationToken);
        if (bytes is null || bytes.Length == 0)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.NotFound, stamp);

        var envelope = JsonSerializer.Deserialize<PermissionCacheEnvelope>(bytes, JsonOptions);
        if (envelope?.Keys is null)
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.NotFound, stamp);

        if (envelope.ProfileId != profileId)
            return new PermissionsCacheReadResult(
                null,
                PermissionsCacheMissReason.ProfileMismatch,
                stamp
            );

        if (
            envelope.TenantVersion != stamp.TenantVersion
            || envelope.CompanyVersion != stamp.CompanyVersion
            || envelope.UserVersion != stamp.UserVersion
        )
            return new PermissionsCacheReadResult(
                null,
                PermissionsCacheMissReason.VersionMismatch,
                stamp
            );

        return new PermissionsCacheReadResult(envelope.Keys, null, stamp);
    }

    public async Task WriteAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        IReadOnlyList<string> keys,
        PermissionsCacheStamp stamp,
        CancellationToken cancellationToken = default
    )
    {
        if (Stopwatch.GetElapsedTime(stamp.ObservedAtTimestamp) > MaxStampAge)
            return;

        var envelope = new PermissionCacheEnvelope(
            profileId,
            stamp.TenantVersion,
            stamp.CompanyVersion,
            stamp.UserVersion,
            keys.ToList()
        );

        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        await _cache.SetAsync(
            DataKey(companyId, userId),
            bytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(DefaultTtlSeconds),
            },
            cancellationToken
        );
    }

    public Task<bool> TryBeginAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    ) => Succeeded(_versions.BeginAsync(scope.Key));

    public Task<bool> TryCompleteAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    ) => Succeeded(_versions.CompleteAsync(scope.Key));

    // Sin capturar: los fallos los traduce a false ResilientPermissionsCacheService (con log).
    private static async Task<bool> Succeeded(Task operation)
    {
        await operation;
        return true;
    }

    private static string[] ScopeKeys(Guid tenantId, Guid companyId, Guid userId) =>
        [
            PermissionsCacheScope.Tenant(tenantId).Key,
            PermissionsCacheScope.Company(companyId).Key,
            PermissionsCacheScope.User(companyId, userId).Key,
        ];

    private static string DataKey(Guid companyId, Guid userId) =>
        $"permissions:{companyId:N}:{userId:N}";

    private sealed record PermissionCacheEnvelope(
        Guid ProfileId,
        long TenantVersion,
        long CompanyVersion,
        long UserVersion,
        List<string> Keys
    );
}

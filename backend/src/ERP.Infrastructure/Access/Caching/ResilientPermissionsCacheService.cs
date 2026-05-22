using ERP.Application.Access.Caching;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// Decorador que nunca propaga fallos de cache — degradación silenciosa a BD.
/// </summary>
public sealed class ResilientPermissionsCacheService : IPermissionsCacheBackend
{
    private readonly DistributedPermissionsCacheService _inner;
    private readonly IPermissionsCacheDiagnostics _diagnostics;
    private readonly ILogger<ResilientPermissionsCacheService> _logger;

    public ResilientPermissionsCacheService(
        DistributedPermissionsCacheService inner,
        IPermissionsCacheDiagnostics diagnostics,
        ILogger<ResilientPermissionsCacheService> logger)
    {
        _inner = inner;
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public async Task<PermissionsCacheReadResult> ReadAsync(
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        CancellationToken ct = default)
    {
        try
        {
            return await _inner.ReadAsync(subscriberId, companyId, userId, ct);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            _logger.LogWarning(ex, "Permissions cache READ failed for {CompanyId}/{UserId}; falling back to DB.", companyId, userId);
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.Exception);
        }
    }

    public async Task WriteAsync(
        Guid subscriberId,
        Guid companyId,
        Guid userId,
        IReadOnlyList<string> keys,
        TimeSpan? ttl = null,
        CancellationToken ct = default)
    {
        try
        {
            await _inner.WriteAsync(subscriberId, companyId, userId, keys, ttl, ct);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            _logger.LogWarning(ex, "Permissions cache WRITE failed for {CompanyId}/{UserId}; ignored.", companyId, userId);
        }
    }

    public async Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken ct = default)
    {
        try
        {
            await _inner.InvalidateUserAsync(companyId, userId, ct);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            _logger.LogWarning(ex, "Permissions cache INVALIDATE user failed for {CompanyId}/{UserId}; ignored.", companyId, userId);
        }
    }

    public async Task BumpCompanyVersionAsync(Guid companyId, CancellationToken ct = default)
    {
        try
        {
            await _inner.BumpCompanyVersionAsync(companyId, ct);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            _logger.LogWarning(ex, "Permissions cache BUMP company failed for {CompanyId}; ignored.", companyId);
        }
    }

    public async Task BumpSubscriberVersionAsync(Guid subscriberId, CancellationToken ct = default)
    {
        try
        {
            await _inner.BumpSubscriberVersionAsync(subscriberId, ct);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            _logger.LogWarning(ex, "Permissions cache BUMP subscriber failed for {SubscriberId}; ignored.", subscriberId);
        }
    }
}

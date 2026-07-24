using ERP.Application.Access.Caching;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// Decorador que nunca propaga fallos de cache — degradación silenciosa a BD.
/// </summary>
public sealed partial class ResilientPermissionsCacheService : IPermissionsCacheBackend
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
        Guid tenantId,
        Guid companyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _inner.ReadAsync(tenantId, companyId, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogCacheReadFailed(ex, companyId, userId);
            return new PermissionsCacheReadResult(null, PermissionsCacheMissReason.Exception);
        }
    }

    public async Task WriteAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        IReadOnlyList<string> keys,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.WriteAsync(tenantId, companyId, userId, keys, ttl, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogCacheWriteFailed(ex, companyId, userId);
        }
    }

    public async Task InvalidateUserAsync(Guid companyId, Guid userId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.InvalidateUserAsync(companyId, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogCacheInvalidateFailed(ex, companyId, userId);
        }
    }

    public async Task BumpCompanyVersionAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.BumpCompanyVersionAsync(companyId, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogCacheBumpCompanyFailed(ex, companyId);
        }
    }

    public async Task BumpTenantVersionAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        try
        {
            await _inner.BumpTenantVersionAsync(tenantId, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogCacheBumpTenantFailed(ex, tenantId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permissions cache READ failed for {CompanyId}/{UserId}; falling back to DB.")]
    private partial void LogCacheReadFailed(Exception ex, Guid companyId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permissions cache WRITE failed for {CompanyId}/{UserId}; ignored.")]
    private partial void LogCacheWriteFailed(Exception ex, Guid companyId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permissions cache INVALIDATE user failed for {CompanyId}/{UserId}; ignored.")]
    private partial void LogCacheInvalidateFailed(Exception ex, Guid companyId, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permissions cache BUMP company failed for {CompanyId}; ignored.")]
    private partial void LogCacheBumpCompanyFailed(Exception ex, Guid companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Permissions cache BUMP tenant failed for {TenantId}; ignored.")]
    private partial void LogCacheBumpTenantFailed(Exception ex, Guid tenantId);
}

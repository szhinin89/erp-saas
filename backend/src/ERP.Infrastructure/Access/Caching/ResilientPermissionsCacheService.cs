using ERP.Application.Access.Caching;
using Microsoft.Extensions.Logging;

namespace ERP.Infrastructure.Access.Caching;

/// <summary>
/// Decorador que nunca propaga fallos de cache. SEC-06 — cada fallo degrada hacia el lado seguro:
/// <list type="bullet">
/// <item>Lectura: miss sin sello → el consumidor resuelve desde BD y no publica.</item>
/// <item>Escritura: se ignora; no publicar nunca concede permisos.</item>
/// <item>Invalidación: <c>false</c>, visible (log Error + métrica). Begin fallido impide persistir;
/// Complete fallido deja la marca de invalidación en curso (ver <see cref="IPermissionsCacheInvalidator"/>).</item>
/// </list>
/// </summary>
public sealed partial class ResilientPermissionsCacheService : IPermissionsCacheBackend
{
    private readonly DistributedPermissionsCacheService _inner;
    private readonly IPermissionsCacheDiagnostics _diagnostics;
    private readonly ILogger<ResilientPermissionsCacheService> _logger;

    public ResilientPermissionsCacheService(
        DistributedPermissionsCacheService inner,
        IPermissionsCacheDiagnostics diagnostics,
        ILogger<ResilientPermissionsCacheService> logger
    )
    {
        _inner = inner;
        _diagnostics = diagnostics;
        _logger = logger;
    }

    public async Task<PermissionsCacheReadResult> ReadAsync(
        Guid tenantId,
        Guid companyId,
        Guid userId,
        Guid profileId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _inner.ReadAsync(tenantId, companyId, userId, profileId, cancellationToken);
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
        Guid profileId,
        IReadOnlyList<string> keys,
        PermissionsCacheStamp stamp,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await _inner.WriteAsync(
                tenantId,
                companyId,
                userId,
                profileId,
                keys,
                stamp,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogCacheWriteFailed(ex, companyId, userId);
        }
    }

    public async Task<bool> TryBeginAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _inner.TryBeginAsync(scope, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogInvalidationBeginFailed(ex, scope.Key);
            return false;
        }
    }

    public async Task<bool> TryCompleteAsync(
        PermissionsCacheScope scope,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            return await _inner.TryCompleteAsync(scope, cancellationToken);
        }
        catch (Exception ex)
        {
            _diagnostics.RecordError();
            LogInvalidationCompleteFailed(ex, scope.Key);
            return false;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Permissions cache READ failed for {CompanyId}/{UserId}; falling back to DB."
    )]
    private partial void LogCacheReadFailed(Exception ex, Guid companyId, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Permissions cache WRITE failed for {CompanyId}/{UserId}; ignored (nothing published)."
    )]
    private partial void LogCacheWriteFailed(Exception ex, Guid companyId, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Permissions cache invalidation BEGIN failed for {Scope}; mutation must not be persisted."
    )]
    private partial void LogInvalidationBeginFailed(Exception ex, string scope);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Permissions cache invalidation COMPLETE failed for {Scope}; scope stays in protected (cache-bypass) mode until the pending mark expires."
    )]
    private partial void LogInvalidationCompleteFailed(Exception ex, string scope);
}

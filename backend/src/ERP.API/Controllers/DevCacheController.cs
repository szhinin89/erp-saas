using ERP.API.Contracts;
using ERP.API.Diagnostics;
using ERP.API.Extensions;
using ERP.Application.Access.Caching;
using ERP.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Controllers;

/// <summary>Endpoints DEV ONLY para auditoría de Redis / cache distribuido.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevCacheController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly CacheHealthProbe _probe;
    private readonly ICacheProviderStatus _providerStatus;
    private readonly ICacheDiagnosticsMetrics _metrics;
    private readonly IPermissionsCacheDiagnostics _permissionsMetrics;

    public DevCacheController(
        IWebHostEnvironment environment,
        CacheHealthProbe probe,
        ICacheProviderStatus providerStatus,
        ICacheDiagnosticsMetrics metrics,
        IPermissionsCacheDiagnostics permissionsMetrics
    )
    {
        _environment = environment;
        _probe = probe;
        _providerStatus = providerStatus;
        _metrics = metrics;
        _permissionsMetrics = permissionsMetrics;
    }

    /// <summary>Valida conexión Redis, write/read y latencia simple (sonda: <see cref="CacheHealthProbe"/>).</summary>
    [HttpGet("/api/dev/redis-health")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRedisHealth(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return this.ApiNotFound("Endpoint disponible solo en Development.");

        return this.ApiOk(await _probe.CheckAsync(cancellationToken));
    }

    /// <summary>Métricas in-process de cache (hits, misses, hit ratio).</summary>
    [HttpGet("/api/dev/cache-metrics")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetCacheMetrics()
    {
        if (!_environment.IsDevelopment())
            return this.ApiNotFound("Endpoint disponible solo en Development.");

        var snapshot = _metrics.GetSnapshot();
        var permissions = _permissionsMetrics.GetSnapshot();
        return this.ApiOk(
            new
            {
                cache_hit_total = snapshot.Hits,
                cache_miss_total = snapshot.Misses,
                cache_set_total = snapshot.Sets,
                hitRatio = snapshot.HitRatio,
                hitsByCategory = snapshot.HitsByCategory,
                missesByCategory = snapshot.MissesByCategory,
                permissions = new
                {
                    cache_hit_total = permissions.CacheHitTotal,
                    cache_miss_total = permissions.CacheMissTotal,
                    cache_set_total = permissions.CacheSetTotal,
                    cache_error_total = permissions.CacheErrorTotal,
                    hitRatio = permissions.HitRatio,
                    miss_reason = permissions.MissesByReason,
                },
                provider = _providerStatus.ProviderName,
                fallbackActive = _providerStatus.FallbackActive,
                redisConfigured = _providerStatus.RedisConfigured,
            }
        );
    }
}

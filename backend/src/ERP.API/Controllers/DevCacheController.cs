using ERP.Application.Access.Caching;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace ERP.API.Controllers;

/// <summary>Endpoints DEV ONLY para auditoría de Redis / cache distribuido.</summary>
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevCacheController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;
    private readonly ICacheProviderStatus _providerStatus;
    private readonly ICacheDiagnosticsMetrics _metrics;
    private readonly IPermissionsCacheDiagnostics _permissionsMetrics;

    public DevCacheController(
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IDistributedCache cache,
        ICacheProviderStatus providerStatus,
        ICacheDiagnosticsMetrics metrics,
        IPermissionsCacheDiagnostics permissionsMetrics)
    {
        _environment = environment;
        _configuration = configuration;
        _cache = cache;
        _providerStatus = providerStatus;
        _metrics = metrics;
        _permissionsMetrics = permissionsMetrics;
    }

    /// <summary>Valida conexión Redis, write/read y latencia simple.</summary>
    [HttpGet("/api/dev/redis-health")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRedisHealth(CancellationToken ct)
    {
        if (!_environment.IsDevelopment())
            return this.ApiNotFound("Endpoint disponible solo en Development.");

        var redisConnection = ResolveRedisConnectionString();
        var redisConfigured = !string.IsNullOrWhiteSpace(redisConnection);
        var probeKey = "erp:health:probe";
        var probeValue = Guid.NewGuid().ToString("N");

        var writeReadOk = false;
        long writeReadLatencyMs = -1;
        long? redisPingMs = null;
        var redisConnected = false;

        if (redisConfigured)
        {
            try
            {
                var mux = await ConnectionMultiplexer.ConnectAsync(redisConnection!);
                redisConnected = mux.IsConnected;
                redisPingMs = (long)mux.GetDatabase().Ping().TotalMilliseconds;
            }
            catch
            {
                redisConnected = false;
            }
        }

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _cache.SetStringAsync(
                probeKey,
                probeValue,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15),
                },
                ct);
            var read = await _cache.GetStringAsync(probeKey, ct);
            sw.Stop();
            writeReadLatencyMs = sw.ElapsedMilliseconds;
            writeReadOk = string.Equals(read, probeValue, StringComparison.Ordinal);
            await _cache.RemoveAsync(probeKey, ct);
        }
        catch
        {
            writeReadOk = false;
        }

        return this.ApiOk(new
        {
            redisConfigured,
            redisConnected = redisConfigured && redisConnected,
            fallbackActive = _providerStatus.FallbackActive,
            provider = _providerStatus.ProviderName,
            writeReadOk,
            latencyMs = writeReadLatencyMs,
            redisPingMs,
            instanceName = _configuration["Redis:InstanceName"] ?? "ERP_",
        });
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
        return this.ApiOk(new
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
        });
    }

    private string? ResolveRedisConnectionString()
        => _configuration["Redis:ConnectionString"]
           ?? _configuration.GetConnectionString("Redis");
}

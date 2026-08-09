using ERP.Application.Common;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ERP.Application.Behaviors;

/// <summary>
/// Intercepta <see cref="ICacheable"/> y devuelve respuesta desde Redis/memoria si existe.
/// Debe registrarse después de validación y límites de suscripción para no omitirlos en cache miss.
/// </summary>
public sealed partial class CachingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IDistributedCache _cache;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        IDistributedCache cache,
        ICurrentTenant tenant,
        ILogger<CachingBehavior<TRequest, TResponse>> logger
    )
    {
        _cache = cache;
        _currentTenant = tenant;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request is not ICacheable cacheable)
            return await next(cancellationToken);

        var tenantId = _currentTenant.TenantId;
        var tenantSegment = tenantId == Guid.Empty ? "no-tenant" : tenantId.ToString("N");
        var payload = JsonSerializer.Serialize(request, request.GetType(), JsonOptions);
        var cacheKey = $"Query:{typeof(TRequest).FullName}:{tenantSegment}:{payload}";

        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<TResponse>(cached, JsonOptions);
                if (deserialized is not null)
                {
                    LogCacheHit(typeof(TRequest).Name);
                    return deserialized;
                }
            }
            catch (JsonException ex)
            {
                LogCacheCorrupted(ex, typeof(TRequest).Name);
            }
        }

        LogCacheMiss(typeof(TRequest).Name);
        var response = await next(cancellationToken);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(cacheable.CacheTTL),
        };
        try
        {
            var serialized = JsonSerializer.Serialize(response, JsonOptions);
            await _cache.SetStringAsync(cacheKey, serialized, options, cancellationToken);
        }
        catch (JsonException ex)
        {
            LogCacheSerializationFailed(ex, typeof(TRequest).Name);
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache hit for {Request}")]
    private partial void LogCacheHit(string request);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Cache corrupta para {Request}; se recalcula."
    )]
    private partial void LogCacheCorrupted(Exception ex, string request);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cache miss for {Request}")]
    private partial void LogCacheMiss(string request);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No se pudo serializar respuesta en caché para {Request}."
    )]
    private partial void LogCacheSerializationFailed(Exception ex, string request);
}

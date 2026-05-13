using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using ERP.Application.Common;

namespace ERP.Application.Behaviors;

/// <summary>
/// Intercepta <see cref="ICacheable"/> y devuelve respuesta desde Redis/memoria si existe.
/// Debe registrarse después de validación y límites de suscripción para no omitirlos en cache miss.
/// </summary>
public sealed class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IDistributedCache _cache;
    private readonly ICurrentTenant _tenant;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        IDistributedCache cache,
        ICurrentTenant tenant,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _tenant = tenant;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheable cacheable)
            return await next();

        var tenantId = _tenant.TenantId;
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
                    _logger.LogInformation("Cache hit for {Request}", typeof(TRequest).Name);
                    return deserialized;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Cache corrupta para {Request}; se recalcula.", typeof(TRequest).Name);
            }
        }

        _logger.LogInformation("Cache miss for {Request}", typeof(TRequest).Name);
        var response = await next();
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
            _logger.LogWarning(ex, "No se pudo serializar respuesta en caché para {Request}.", typeof(TRequest).Name);
        }

        return response;
    }
}

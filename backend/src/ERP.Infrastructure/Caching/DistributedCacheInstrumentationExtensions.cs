using ERP.Application.Common.Interfaces;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Caching;

public static class DistributedCacheInstrumentationExtensions
{
    public static IServiceCollection AddDistributedCacheInstrumentation(
        this IServiceCollection services,
        IConfiguration configuration,
        bool redisConfigured)
    {
        services.Configure<CacheDiagnosticsOptions>(configuration.GetSection(CacheDiagnosticsOptions.SectionName));
        services.TryAddSingleton<CacheDiagnosticsMetrics>();
        services.TryAddSingleton<ICacheDiagnosticsMetrics>(sp => sp.GetRequiredService<CacheDiagnosticsMetrics>());
        services.TryAddSingleton<ICacheProviderStatus>(_ => new CacheProviderStatus(
            redisConfigured ? "Redis" : "Memory",
            redisConfigured,
            fallbackActive: !redisConfigured));

        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IDistributedCache));
        if (descriptor is null)
            return services;

        services.Remove(descriptor);
        services.AddSingleton<IDistributedCache>(sp =>
        {
            var inner = ResolveInnerCache(sp, descriptor);
            return new InstrumentedDistributedCache(
                inner,
                sp.GetRequiredService<ICacheDiagnosticsMetrics>(),
                sp.GetRequiredService<IOptions<CacheDiagnosticsOptions>>(),
                sp.GetRequiredService<ILogger<InstrumentedDistributedCache>>());
        });

        return services;
    }

    private static IDistributedCache ResolveInnerCache(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is IDistributedCache instance)
            return instance;

        if (descriptor.ImplementationFactory is { } factory)
            return (IDistributedCache)factory(sp);

        if (descriptor.ImplementationType is { } type)
            return (IDistributedCache)ActivatorUtilities.CreateInstance(sp, type);

        throw new InvalidOperationException("No se pudo resolver la implementación interna de IDistributedCache.");
    }
}

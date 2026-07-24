using ERP.Application.Common.Interfaces;

namespace ERP.Infrastructure.Caching;

public sealed class CacheProviderStatus : ICacheProviderStatus
{
    public CacheProviderStatus(string providerName, bool redisConfigured, bool fallbackActive)
    {
        ProviderName = providerName;
        RedisConfigured = redisConfigured;
        FallbackActive = fallbackActive;
    }

    public string ProviderName { get; }
    public bool RedisConfigured { get; }
    public bool FallbackActive { get; }
}

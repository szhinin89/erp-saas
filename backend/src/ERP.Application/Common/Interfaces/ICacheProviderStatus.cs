namespace ERP.Application.Common.Interfaces;

/// <summary>Indica qué proveedor de IDistributedCache está activo en runtime.</summary>
public interface ICacheProviderStatus
{
    string ProviderName { get; }
    bool RedisConfigured { get; }
    bool FallbackActive { get; }
}

namespace ERP.Application.Admin;

/// <summary>
/// Servicio central de configuración jerárquica por tenant.
/// Prioridad: Feature -&gt; Module -&gt; Global (y opcionalmente Usuario en futuras iteraciones).
/// </summary>
public interface IConfigService
{
    Task WarmupTenantAsync(Guid subscriberId, CancellationToken ct = default);

    Task<ResolvedConfigValueDto?> GetValueAsync(
        Guid subscriberId,
        string key,
        string? module = null,
        string? feature = null,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<T?> GetValueTypedAsync<T>(
        Guid subscriberId,
        string key,
        string? module = null,
        string? feature = null,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConfigEntryDto>> ListGlobalAsync(Guid subscriberId, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigEntryDto>> ListModuleAsync(Guid subscriberId, string module, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigEntryDto>> ListFeatureAsync(Guid subscriberId, string feature, CancellationToken ct = default);

    Task<ConfigEntryDto> UpsertGlobalAsync(Guid subscriberId, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default);
    Task<ConfigEntryDto> UpsertModuleAsync(Guid subscriberId, string module, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default);
    Task<ConfigEntryDto> UpsertFeatureAsync(Guid subscriberId, string feature, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default);

    Task<bool> DeleteGlobalAsync(Guid subscriberId, string key, CancellationToken ct = default);
    Task<bool> DeleteModuleAsync(Guid subscriberId, string module, string key, CancellationToken ct = default);
    Task<bool> DeleteFeatureAsync(Guid subscriberId, string feature, string key, CancellationToken ct = default);
}


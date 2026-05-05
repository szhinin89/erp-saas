namespace ERP.Application.Admin;

/// <summary>
/// Servicio central de configuración jerárquica por tenant.
/// Prioridad: Feature -&gt; Module -&gt; Global (y opcionalmente Usuario en futuras iteraciones).
/// </summary>
public interface IConfigService
{
    Task WarmupTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task<ResolvedConfigValueDto?> GetValueAsync(
        Guid tenantId,
        string key,
        string? module = null,
        string? feature = null,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<T?> GetValueTypedAsync<T>(
        Guid tenantId,
        string key,
        string? module = null,
        string? feature = null,
        Guid? userId = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConfigEntryDto>> ListGlobalAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigEntryDto>> ListModuleAsync(Guid tenantId, string module, CancellationToken ct = default);
    Task<IReadOnlyList<ConfigEntryDto>> ListFeatureAsync(Guid tenantId, string feature, CancellationToken ct = default);

    Task<ConfigEntryDto> UpsertGlobalAsync(Guid tenantId, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default);
    Task<ConfigEntryDto> UpsertModuleAsync(Guid tenantId, string module, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default);
    Task<ConfigEntryDto> UpsertFeatureAsync(Guid tenantId, string feature, string key, string value, string dataType, Guid updatedBy, CancellationToken ct = default);

    Task<bool> DeleteGlobalAsync(Guid tenantId, string key, CancellationToken ct = default);
    Task<bool> DeleteModuleAsync(Guid tenantId, string module, string key, CancellationToken ct = default);
    Task<bool> DeleteFeatureAsync(Guid tenantId, string feature, string key, CancellationToken ct = default);
}


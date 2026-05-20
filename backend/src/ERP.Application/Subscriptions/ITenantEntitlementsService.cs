using ERP.Domain.Subscriptions;

namespace ERP.Application.Subscriptions;

/// <summary>
/// Fuente única de verdad para entitlements comerciales del tenant (módulos y features).
/// Basado en suscripción activa, plan y overrides — no usa <c>EnabledModulesJson</c> ni catálogo legacy.
/// </summary>
public interface ITenantEntitlementsService
{
    /// <summary>
    /// Claves de módulo habilitadas por plan/overrides (<see cref="SaasFeatureKind.Module"/>).
    /// Sin suscripción activa → colección vacía (fail-closed).
    /// </summary>
    Task<IReadOnlyCollection<string>> GetEnabledModuleKeysAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Indica si el tenant tiene la feature comercial (<c>saas_feature_definitions.code</c>).
    /// Sin suscripción activa → <c>false</c>.
    /// </summary>
    Task<bool> HasFeatureAsync(Guid tenantId, string featureCode, CancellationToken ct = default);

    /// <summary>
    /// Límite efectivo por periodo para una feature medida; <c>null</c> si ilimitado o no aplica.
    /// </summary>
    Task<int?> GetLimitPerPeriodAsync(Guid tenantId, string featureCode, CancellationToken ct = default);

    /// <summary>
    /// Indica si el permiso RBAC (<c>{module}.{resource}.{action}</c>) está permitido por el plan del tenant.
    /// Prefijos no asociados a un módulo comercial → <c>true</c> (no gated por suscripción).
    /// </summary>
    Task<bool> AllowsPermissionAsync(Guid tenantId, string permissionKey, CancellationToken ct = default);
}

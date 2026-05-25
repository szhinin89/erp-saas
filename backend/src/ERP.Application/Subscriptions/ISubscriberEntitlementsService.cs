using ERP.Domain.Subscriptions;

namespace ERP.Application.Subscriptions;

/// <summary>
/// Fuente única de verdad para entitlements comerciales del tenant (módulos y features).
/// Basado en suscripción activa, plan y overrides — no usa <c>EnabledModulesJson</c> ni catálogo legacy.
/// </summary>
public interface ISubscriberEntitlementsService
{
    /// <summary>
    /// Claves de módulo habilitadas por plan/overrides (<see cref="PlatformFeatureKind.Module"/>).
    /// Sin suscripción activa → colección vacía (fail-closed).
    /// </summary>
    Task<IReadOnlyCollection<string>> GetEnabledModuleKeysAsync(Guid subscriberId, CancellationToken ct = default);

    /// <summary>
    /// Indica si el tenant tiene la feature comercial (<c>platform_features.code</c>).
    /// Sin suscripción activa → <c>false</c>.
    /// </summary>
    Task<bool> HasFeatureAsync(Guid subscriberId, string featureCode, CancellationToken ct = default);

    /// <summary>
    /// Límite efectivo por periodo para una feature medida; <c>null</c> si ilimitado o no aplica.
    /// </summary>
    Task<int?> GetLimitPerPeriodAsync(Guid subscriberId, string featureCode, CancellationToken ct = default);

    /// <summary>
    /// Indica si el permiso RBAC (<c>{module}.{resource}.{action}</c>) está permitido por el plan del tenant.
    /// Prefijos no asociados a un módulo comercial → <c>true</c> (no gated por suscripción).
    /// </summary>
    Task<bool> AllowsPermissionAsync(Guid subscriberId, string permissionKey, CancellationToken ct = default);

    /// <summary>
    /// Snapshot único para UI: módulos, features comerciales y límites medidos.
    /// Sin suscripción activa → listas vacías y <see cref="SubscriberEntitlementsSnapshot.HasModuleRestrictions"/> = true.
    /// </summary>
    Task<SubscriberEntitlementsSnapshot> GetEntitlementsSnapshotAsync(Guid subscriberId, CancellationToken ct = default);
}

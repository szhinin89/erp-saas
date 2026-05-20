namespace ERP.Application.Subscriptions;

/// <summary>
/// Sincroniza restricciones de módulos vía <c>tenant_subscription_feature_overrides</c>
/// (no escribe <c>EnabledModulesJson</c>).
/// </summary>
public interface ITenantSubscriptionOverridesService
{
    /// <summary>
    /// <paramref name="requestedModuleKeys"/> null o vacío → quita overrides de módulo (solo plan).
    /// Lista no vacía → override por feature <see cref="SaasFeatureKind.Module"/> del plan activo.
    /// </summary>
    Task ApplyModuleOverridesAsync(
        Guid tenantId,
        IReadOnlyList<string>? requestedModuleKeys,
        Guid actorId,
        CancellationToken ct = default);
}

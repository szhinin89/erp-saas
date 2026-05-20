namespace ERP.Application.Subscriptions;

/// <summary>
/// Sincroniza restricciones de módulos vía <c>subscription_feature_overrides</c>
/// (no escribe <c>EnabledModulesJson</c>).
/// </summary>
public interface ISubscriptionFeatureOverridesService
{
    /// <summary>
    /// <paramref name="requestedModuleKeys"/> null o vacío → quita overrides de módulo (solo plan).
    /// Lista no vacía → override por feature <see cref="PlatformFeatureKind.Module"/> del plan activo.
    /// </summary>
    Task ApplyModuleOverridesAsync(
        Guid subscriberId,
        IReadOnlyList<string>? requestedModuleKeys,
        Guid actorId,
        CancellationToken ct = default);
}

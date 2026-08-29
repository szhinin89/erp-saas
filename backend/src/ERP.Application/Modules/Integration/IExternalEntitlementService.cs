namespace ERP.Application.Modules.Integration;

/// <summary>
/// SECURITY-PERMISSION-SCOPE-01 — puerto/adaptador (Ports &amp; Adapters) hacia una futura
/// plataforma SaaS EXTERNA, conectada al ERP Core únicamente por API. Este contrato es
/// deliberadamente el único punto de contacto — el ERP Core nunca conoce ni depende de cómo esa
/// plataforma modela planes/suscripciones/precios internamente.
///
/// Qué NO es esta interfaz (para que quede explícito y no se reinterprete en un futuro ticket):
/// - No es un modelo de planes internos del ERP Core — no hay ni debe haber <c>RequiredPlan</c>,
///   <c>PlanKey</c>, <c>SubscriptionId</c>, <c>BillingCycle</c>, <c>CommercialPlan</c> ni entidad
///   de planes/precios comerciales en <c>ERP.Domain</c>/<c>ERP.Application</c> — ver
///   <c>ERP.Architecture.Tests.PlatformControlPlaneGuardTests.ERP_domain_must_not_reference_saas_namespaces</c>,
///   que falla el build si aparecen esos términos en <c>ERP.Domain</c>.
/// - No implementa billing ni facturación de la suscripción SaaS (eso es 100% de la plataforma
///   externa, fuera del alcance y del repositorio del ERP Core).
/// - No implementa suscripciones (altas/bajas/renovaciones de plan) — el ERP Core no las modela.
/// - No bloquea nada por plan hoy: la única implementación registrada,
///   <see cref="NoOpExternalEntitlementService"/>, es permisiva siempre. Ningún handler de este
///   ticket (p. ej. <c>UpsertProfilePermissionsHandler</c>) consulta este servicio todavía — el
///   único consumo actual es la metadata declarativa opcional
///   <c>NavItemAttribute.FeatureKey</c>/<c>RequiresExternalEntitlement</c>, que tampoco gatea nada.
/// - Es únicamente un seam (costura de extensión) para que, cuando exista la plataforma SaaS
///   externa, se reemplace solo el adaptador (una nueva implementación de esta interfaz que llame
///   a su API real) — sin refactorizar menú, permisos ni handlers principales del ERP Core.
/// </summary>
public interface IExternalEntitlementService
{
    /// <summary>
    /// Indica si <paramref name="featureKey"/> está habilitado por el entitlement externo del
    /// tenant, resuelto por la plataforma SaaS externa (nunca calculado ni almacenado en el ERP
    /// Core). <paramref name="featureKey"/> corresponde a
    /// <c>NavItemAttribute.FeatureKey</c>/<c>NavigationItemDefinition.FeatureKey</c>.
    /// </summary>
    Task<bool> IsFeatureEnabledAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Única implementación registrada hoy: NoOp/permisiva. No hay plataforma SaaS externa conectada
/// todavía, así que ningún feature se bloquea por plan — <see cref="IsFeatureEnabledAsync"/>
/// siempre devuelve <c>true</c>. No modela planes, billing ni suscripciones (ver la nota en
/// <see cref="IExternalEntitlementService"/>). Reemplazar por el adaptador real de la plataforma
/// SaaS externa cuando exista — sin cambiar el contrato ni sus consumidores.
/// </summary>
public sealed class NoOpExternalEntitlementService : IExternalEntitlementService
{
    public Task<bool> IsFeatureEnabledAsync(
        Guid tenantId,
        string featureKey,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(true);
}

namespace ERP.Application.Common.Config;

/// <summary>
/// Flags operativos de entitlements SaaS. Sección <c>Subscriber:Entitlements</c> en appsettings.
/// </summary>
public sealed class SubscriberEntitlementsOptions
{
    public const string Section = "Subscriber:Entitlements";

    /// <summary>
    /// Si <c>true</c> (default), sin suscripción activa no hay módulos ni features (fail-closed).
    /// Desactivar solo en diagnóstico temporal; no usar en producción.
    /// </summary>
    public bool FailClosedWithoutActiveSubscription { get; set; } = true;

    /// <summary>
    /// Log debug en cada consulta sin filtro de suscriptor vía <see cref="IPlatformQueryAccessor"/>.
    /// </summary>
    public bool LogPlatformQueries { get; set; } = false;
}

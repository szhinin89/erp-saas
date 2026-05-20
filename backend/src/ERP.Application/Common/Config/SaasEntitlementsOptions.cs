namespace ERP.Application.Common.Config;

/// <summary>
/// Flags operativos de entitlements SaaS (Fase A/B). Sección <c>Saas:Entitlements</c> en appsettings.
/// </summary>
public sealed class SaasEntitlementsOptions
{
    public const string Section = "Saas:Entitlements";

    /// <summary>
    /// Si <c>true</c> (default), sin suscripción activa no hay módulos ni features (fail-closed).
    /// Desactivar solo en diagnóstico temporal; no usar en producción.
    /// </summary>
    public bool FailClosedWithoutActiveSubscription { get; set; } = true;

    /// <summary>
    /// Log debug en cada consulta sin filtro de tenant vía <see cref="IPlatformQueryAccessor"/>.
    /// </summary>
    public bool LogPlatformQueries { get; set; } = false;
}

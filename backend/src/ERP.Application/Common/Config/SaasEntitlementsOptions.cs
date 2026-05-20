namespace ERP.Application.Common.Config;

/// <summary>
/// Flags operativos de entitlements SaaS (Fase A). Sección <c>Saas:Entitlements</c> en appsettings.
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
    /// Emergencia: JWT/sesión lee <c>Tenant.EnabledModulesJson</c> en lugar del modelo relacional.
    /// Revierte el comportamiento de iteraciones 02–03 para módulos de sesión únicamente.
    /// </summary>
    public bool PreferLegacyEnabledModulesJsonForSession { get; set; } = false;

    /// <summary>
    /// Log debug en cada consulta sin filtro de tenant vía <see cref="IPlatformQueryAccessor"/>.
    /// </summary>
    public bool LogPlatformQueries { get; set; } = false;
}

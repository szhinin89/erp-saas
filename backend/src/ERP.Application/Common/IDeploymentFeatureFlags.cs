namespace ERP.Application.Common;

/// <summary>
/// Banderas de despliegue leídas de configuración (p. ej. instancia on-prem con panel SuperAdmin cerrado tras la puesta en marcha).
/// Los topes numéricos pueden sobrescribirse con <c>App_Data/instance-quota.json</c> (ver <see cref="InstanceQuotaFileModel"/>).
/// </summary>
public interface IDeploymentFeatureFlags
{
    /// <summary>
    /// Si es false, no se permite login global ni operaciones de panel SuperAdmin (config <c>Deployment:SuperAdminPanelEnabled</c>).
    /// </summary>
    bool IsSuperAdminPanelEnabled { get; }

    /// <summary>
    /// Tope de empresas (tenants) activas en la instancia; null = sin límite explícito (ilimitado salvo <see cref="IsDedicatedSingleClientInstance"/>).
    /// </summary>
    int? MaxActiveTenants { get; }

    /// <summary>
    /// Tope de usuarios globales (<c>identity_users</c>); null = sin límite (<c>Deployment:MaxIdentityUsers</c>).
    /// </summary>
    int? MaxIdentityUsers { get; }

    /// <summary>
    /// Instancia dedicada a un solo cliente / servidor propio: no se permite un número ilimitado de empresas (RUC);
    /// debe definirse <see cref="MaxActiveTenants"/> (archivo o configuración).
    /// </summary>
    bool IsDedicatedSingleClientInstance { get; }

    /// <summary>
    /// Tope de usuarios Identity con membresía activa por empresa (tenant). null = sin límite.
    /// </summary>
    int? MaxUsersPerTenant { get; }

    /// <summary>
    /// Alta única del SuperAdmin con token de instalación (<c>Deployment:InitialSuperAdminSetupToken</c>).
    /// Compara en tiempo constante (hash SHA-256); false si no está configurado o el token no coincide.
    /// </summary>
    bool AuthorizeInitialSuperAdminSetup(string? submittedToken);
}

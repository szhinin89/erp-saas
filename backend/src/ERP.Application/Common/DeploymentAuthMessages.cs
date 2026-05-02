namespace ERP.Application.Common;

/// <summary>
/// Prefijo en <see cref="Result{T}.Error"/> para que la API traduzca a HTTP 403.
/// </summary>
public static class DeploymentAuthMessages
{
    public const string ForbiddenPrefix = "FORBIDDEN:";

    public static string SuperAdminPanelDisabledUserMessage =>
        "El panel global de SuperAdmin está deshabilitado en este despliegue. Use usuarios Admin de cada empresa.";

    public static string SuperAdminPanelDisabled =>
        ForbiddenPrefix + " " + SuperAdminPanelDisabledUserMessage;
}

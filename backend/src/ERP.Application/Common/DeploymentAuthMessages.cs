namespace ERP.Application.Common;

/// <summary>
/// Prefijo en <see cref="Result{T}.Error"/> para que la API traduzca a HTTP 403.
/// </summary>
public static class DeploymentAuthMessages
{
    public const string ForbiddenPrefix = "FORBIDDEN:";

    public static string PlatformPanelDisabledUserMessage =>
        "El panel global de operadores platform está deshabilitado en este despliegue. Use usuarios Admin de cada empresa.";

    public static string PlatformPanelDisabled =>
        ForbiddenPrefix + " " + PlatformPanelDisabledUserMessage;
}

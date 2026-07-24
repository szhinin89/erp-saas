namespace ERP.Application.Common.Config;

/// <summary>
/// Opciones de comportamiento del polling de autorización SRI. Se configuran en
/// appsettings.json bajo la sección "SriPolling" — mismo patrón que <see cref="KardexOptions"/>.
/// </summary>
public sealed class SriPollingOptions
{
    public const string Section = "SriPolling";

    /// <summary>
    /// Espera antes de la primera consulta de autorización tras una recepción exitosa —
    /// la ficha técnica offline del SRI (sección 7.4) recomienda dar tiempo al SRI antes de
    /// consultar, en vez de consultar inmediatamente. Configurable por ambiente en
    /// appsettings*.json bajo "SriPolling:InitialAuthorizationDelaySeconds" — nunca hardcodeado
    /// en código (SOAP-01, auditoría SRI Fase 2). El valor entregado en appsettings.json /
    /// appsettings.Production.json es 5 segundos; 0 (el default de este `int` si no se
    /// configura nada) solo se usa como fallback defensivo cuando <see cref="SriSoapClient"/>
    /// se instancia sin <see cref="Microsoft.Extensions.Options.IOptions{TOptions}"/> (p.ej. en
    /// tests unitarios que no ejercitan la espera).
    /// </summary>
    public int InitialAuthorizationDelaySeconds { get; set; }
}

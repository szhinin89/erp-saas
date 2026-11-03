namespace ERP.Application.Common.Config;

/// <summary>
/// SEC-A01 — límites de <c>login</c>/<c>global-login</c> (sección <c>LoginThrottle</c>). Los valores
/// por defecto son candidatos para pruebas locales, no parámetros productivos aprobados.
/// </summary>
public sealed class LoginThrottleOptions
{
    public const string SectionName = "LoginThrottle";

    /// <summary>Duración de la ventana fija compartida por K1, K2 y K3.</summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>K1 — intentos sin éxito por cuenta + IP. No se puede desactivar (mínimo 1).</summary>
    public int AccountIpLimit { get; set; } = 5;

    /// <summary>
    /// K2 — intentos sin éxito por IP. 0 = desactivado: debe seguir así mientras no se verifique que
    /// la API recibe la IP real del cliente (detrás de un proxy todas las solicitudes compartirían IP).
    /// </summary>
    public int IpLimit { get; set; }

    /// <summary>K3 — intentos sin éxito por cuenta desde cualquier IP. 0 = desactivado.</summary>
    public int AccountLimit { get; set; } = 30;

    /// <summary>Logins ejecutándose a la vez por instancia. Nulo o ≤ 0 = número de procesadores.</summary>
    public int? MaxConcurrentVerifications { get; set; }

    /// <summary>Logins en espera por instancia. Nulo o &lt; 0 = 4 × concurrencia.</summary>
    public int? QueueLimit { get; set; }

    /// <summary>Espera máxima en cola antes de responder 429.</summary>
    public int MaxQueueWaitMilliseconds { get; set; } = 2000;
}

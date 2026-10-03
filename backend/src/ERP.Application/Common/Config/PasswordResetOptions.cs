namespace ERP.Application.Common.Config;

/// <summary>Configuración para enlaces de recuperación de contraseña (sección <c>PasswordReset</c>).</summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>URL pública del frontend (sin barra final), p. ej. https://app.ejemplo.com o http://localhost:5173</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Vigencia del token desde su creación (por defecto 60 minutos).</summary>
    public int TokenLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Vigencia del token emitido por LoginHandler cuando RequirePasswordReset está activo
    /// (Fase H) — deliberadamente corta: el usuario ya probó su contraseña temporal en el mismo
    /// request, no es un enlace por email que deba sobrevivir minutos/horas sin uso.
    /// </summary>
    public int FirstLoginTokenLifetimeMinutes { get; set; } = 5;

    /// <summary>
    /// ZH-AUTH-PASSWORD-RESET-SECURITY-HOTFIX-01 — solicitudes de <c>forgot-password</c> aceptadas por
    /// email normalizado dentro de <see cref="IdentityRequestWindowMinutes"/>. Se cuenta ANTES de buscar
    /// la cuenta (mismo comportamiento exista o no); al excederse la solicitud se suprime en silencio
    /// (respuesta neutral, sin token).
    /// </summary>
    public int IdentityRequestLimit { get; set; } = 3;

    public int IdentityRequestWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Límite por IP del endpoint <c>forgot-password</c> (política ASP.NET
    /// <c>auth-forgot-password-ip</c>); al excederse responde 429 <c>RATE_LIMITED</c>.
    /// </summary>
    public int IpRequestLimit { get; set; } = 10;

    public int IpRequestWindowMinutes { get; set; } = 15;
}

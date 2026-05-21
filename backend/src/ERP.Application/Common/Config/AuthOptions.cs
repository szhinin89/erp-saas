namespace ERP.Application.Common.Config;

/// <summary>Opciones de autenticación refresh token (rotación, grace, rate limits).</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Ventana de gracia (segundos) para reuso benigno tras rotación (multi-tab / StrictMode).</summary>
    public int RefreshRotationGraceSeconds { get; set; } = 5;

    /// <summary>Máximo de POST /refresh por IP por minuto.</summary>
    public int RefreshRateLimitPerIpPerMinute { get; set; } = 60;

    /// <summary>Máximo de rotaciones por usuario por minuto.</summary>
    public int RefreshRateLimitPerUserPerMinute { get; set; } = 30;

    /// <summary>Máximo de rotaciones por familia de sesión por minuto.</summary>
    public int RefreshRateLimitPerFamilyPerMinute { get; set; } = 20;
}

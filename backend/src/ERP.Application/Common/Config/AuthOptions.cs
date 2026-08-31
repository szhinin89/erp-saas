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

    /// <summary>
    /// Vida individual de cada refresh token (minutos) antes de necesitar rotación — higiene de
    /// token, no la ventana de sesión. Reemplaza el hardcode previo de 30 días
    /// (<c>RefreshToken.ExpiryDays</c>); en la práctica queda dominado por
    /// <see cref="SessionAbsoluteLifetimeMinutes"/> salvo que este último se configure más largo.
    /// </summary>
    public int RefreshTokenIndividualLifetimeMinutes { get; set; } = 43_200; // 30 días

    /// <summary>
    /// Ventana máxima absoluta de una sesión (minutos) desde su primer login, sin extenderse en
    /// cada rotación de refresh token. Cierra la brecha de "sesión eterna": aunque el usuario siga
    /// usando la app, al superar este límite el refresh falla con 401 y debe autenticarse de nuevo.
    /// </summary>
    public int SessionAbsoluteLifetimeMinutes { get; set; } = 480; // 8 horas
}

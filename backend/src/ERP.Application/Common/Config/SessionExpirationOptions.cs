namespace ERP.Application.Common.Config;

/// <summary>
/// Política de expiración pasiva de UserSession (Fase 9). Independiente de RefreshToken —
/// UserSession nunca lee RefreshToken.ExpiresAt como fuente de verdad, solo su propio
/// StartedAt, por diseño explícito ("No usar RefreshToken como fuente de verdad de sesión").
/// </summary>
public sealed class SessionExpirationOptions
{
    public const string Section = "SessionExpiration";

    /// <summary>Antigüedad máxima (días desde StartedAt) de una UserSession Active antes de expirar.</summary>
    public int MaxSessionAgeDays { get; set; } = 30;
}

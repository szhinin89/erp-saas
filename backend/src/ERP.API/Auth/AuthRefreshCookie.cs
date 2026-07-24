namespace ERP.API.Auth;

/// <summary>
/// Cookie httpOnly del refresh token. Path <c>/api</c> cubre login
/// (<c>/api/auth/login</c>) y refresh (<c>/api/auth/refresh</c>).
/// </summary>
internal static class AuthRefreshCookie
{
    public const string Name = "erp_refresh_token";

    /// <summary>Prefijo común de rutas auth bajo /api.</summary>
    public const string Path = "/api";

    /// <summary>Path legacy (antes de platform login); se limpia en logout por compatibilidad.</summary>
    public const string LegacyPath = "/api/auth";

    public static CookieOptions BuildOptions(HttpRequest request, DateTime expiry)
        => new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = expiry,
            Path = Path,
        };

    public static CookieOptions BuildDeleteOptions(HttpRequest request, string path)
        => new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = path,
        };
}

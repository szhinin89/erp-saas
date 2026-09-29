using ERP.Application.Auth.DTOs;

namespace ERP.API.Auth;

internal static class AuthRefreshCookieHelper
{
    /// <summary>
    /// Emite la cookie de refresh solo si la respuesta de auth trae token y expiración; si no, no
    /// toca la cookie. Única vía de emisión (política en <see cref="AuthRefreshCookie"/>): solo
    /// transporte HTTP, la decisión de emitir o rotar el token es de Application.
    /// </summary>
    public static void SetRefreshCookieIfIssued(HttpContext httpContext, AuthResponseDto? response)
    {
        if (response?.RefreshToken is not null && response.RefreshTokenExpiry is not null)
            SetRefreshCookie(httpContext, response.RefreshToken, response.RefreshTokenExpiry.Value);
    }

    private static void SetRefreshCookie(HttpContext httpContext, string rawToken, DateTime expiry)
    {
        httpContext.Response.Cookies.Append(
            AuthRefreshCookie.Name,
            rawToken,
            AuthRefreshCookie.BuildOptions(httpContext.Request, expiry)
        );
    }

    public static void ClearRefreshCookie(HttpContext httpContext)
    {
        foreach (var path in new[] { AuthRefreshCookie.Path, AuthRefreshCookie.LegacyPath })
        {
            httpContext.Response.Cookies.Delete(
                AuthRefreshCookie.Name,
                AuthRefreshCookie.BuildDeleteOptions(httpContext.Request, path)
            );
        }
    }

    public static string? ResolveRefreshToken(HttpRequest request, string? fromBody) =>
        string.IsNullOrWhiteSpace(fromBody) ? request.Cookies[AuthRefreshCookie.Name] : fromBody;
}

namespace ERP.API.Auth;

internal static class AuthRefreshCookieHelper
{
    public static void SetRefreshCookie(HttpContext httpContext, string rawToken, DateTime expiry)
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

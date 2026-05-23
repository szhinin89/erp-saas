using System.Security.Claims;

namespace ERP.API.Middleware;

/// <summary>
/// Middleware de correlación de seguridad.
///
/// Enriquece el scope de logging de cada request con:
///   - subscriber_id (del JWT)
///   - company_id    (del JWT, si existe)
///   - user_id       (del JWT sub/NameIdentifier)
///   - token_type    (session / bootstrap / platform)
///   - request_id    (HttpContext.TraceIdentifier)
///
/// Esto permite trazar cualquier evento de seguridad (403, cross-company denied,
/// fail-closed trigger) directamente al usuario y contexto que lo causó.
///
/// NO loggea tokens, passwords, ni ningún secreto.
/// Se registra DESPUÉS de la autenticación JWT (app.UseAuthentication).
/// </summary>
public sealed class SecurityCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityCorrelationMiddleware> _logger;

    public SecurityCorrelationMiddleware(
        RequestDelegate next,
        ILogger<SecurityCorrelationMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;

        var subscriberId = user.FindFirst("subscriber_id")?.Value ?? "none";
        var companyId    = user.FindFirst("company_id")?.Value    ?? "none";
        var userId       = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? user.FindFirst("sub")?.Value
                        ?? "none";
        var tokenType    = user.FindFirst("token_type")?.Value    ?? "none";
        var sessionId    = user.FindFirst("session_id")?.Value
                        ?? user.FindFirst("sid")?.Value
                        ?? "none";
        var requestId    = context.TraceIdentifier;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["SubscriberId"] = subscriberId,
            ["CompanyId"]    = companyId,
            ["UserId"]       = userId,
            ["SessionId"]    = sessionId,
            ["TokenType"]    = tokenType,
            ["RequestId"]    = requestId,
        }))
        {
            await _next(context);
        }
    }
}

/// <summary>Extension methods para registrar el middleware en el pipeline.</summary>
public static class SecurityCorrelationMiddlewareExtensions
{
    /// <summary>
    /// Registra SecurityCorrelationMiddleware en el pipeline.
    /// Debe llamarse DESPUÉS de app.UseAuthentication() para que los claims JWT estén disponibles.
    /// </summary>
    public static IApplicationBuilder UseSecurityCorrelation(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityCorrelationMiddleware>();
}

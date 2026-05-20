namespace ERP.API.Middleware;

/// <summary>
/// Registra intentos de acceso denegado (403/401) con contexto mínimo para auditoría.
/// </summary>
public sealed class ForbiddenAccessLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ForbiddenAccessLoggingMiddleware> _logger;

    public ForbiddenAccessLoggingMiddleware(RequestDelegate next, ILogger<ForbiddenAccessLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode is 401 or 403)
        {
            _logger.LogWarning(
                "Access denied {StatusCode} {Method} {Path} remote={RemoteIp}",
                context.Response.StatusCode,
                context.Request.Method,
                context.Request.Path.Value,
                context.Connection.RemoteIpAddress?.ToString());
        }
    }
}

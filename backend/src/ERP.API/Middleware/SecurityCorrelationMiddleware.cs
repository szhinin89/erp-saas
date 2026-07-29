using System.Security.Claims;

namespace ERP.API.Middleware;

/// <summary>
/// Enriquece el scope de logging de cada request con tenant_id (JWT), company_id
/// (X-Company-Id header), user_id (JWT sub) y request_id. NO loggea secretos.
/// Debe registrarse después de app.UseAuthentication().
/// </summary>
public sealed class SecurityCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityCorrelationMiddleware> _logger;

    public SecurityCorrelationMiddleware(
        RequestDelegate next,
        ILogger<SecurityCorrelationMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;

        var tenantId = user.FindFirst("tenant_id")?.Value ?? "none";
        var companyId = context.Request.Headers["X-Company-Id"].FirstOrDefault() ?? "none";
        var userId =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value
            ?? "none";
        var requestId = RequestCorrelationMiddleware.Resolve(context);

        using (
            _logger.BeginScope(
                new Dictionary<string, object>
                {
                    ["TenantId"] = tenantId,
                    ["CompanyId"] = companyId,
                    ["UserId"] = userId,
                    ["RequestId"] = requestId,
                }
            )
        )
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
    public static IApplicationBuilder UseSecurityCorrelation(this IApplicationBuilder app) =>
        app.UseMiddleware<SecurityCorrelationMiddleware>();
}

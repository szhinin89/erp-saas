using ERP.Application.Common;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace ERP.API.Middleware;

/// <summary>
/// Enriquece logs con contexto enterprise (subscriber, company, user, correlation).
/// Emite Warning cuando un endpoint ERP operativo recibe un request autenticado
/// sin company_id en el JWT — útil para detectar handlers que deberían estar
/// company-scoped pero no lo están.
/// </summary>
public sealed class EnterpriseDiagnosticMiddleware
{
    // Prefijos de ruta que legítimamente operan sin company_id.
    private static readonly string[] _noCompanyPrefixes =
    [
        "/api/me",
        "/api/access",
        "/api/auth",
        "/api/setup",
        "/api/platform",
        "/api/public",
        "/api/superadmin",
        "/api/subscribers/entitlements",
        "/api/subscribers",        // gestión SaaS de subscribers
        "/api/dev",
        "/api/health",
        "/hangfire",
        "/swagger",
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<EnterpriseDiagnosticMiddleware> _logger;

    public EnterpriseDiagnosticMiddleware(
        RequestDelegate next,
        ILogger<EnterpriseDiagnosticMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // Detectar request autenticado a ruta ERP operativa sin company_id.
        if (user.IsAuthenticated
            && subscriber.SubscriberId != Guid.Empty
            && !company.HasCompanyContext
            && IsErpOperationalPath(context.Request.Path))
        {
            _logger.LogWarning(
                "ERP_NO_COMPANY_CTX | path={Path} method={Method} subscriber={SubscriberId} user={UserId} role={Role}",
                context.Request.Path.Value,
                context.Request.Method,
                subscriber.SubscriberId,
                user.UserId,
                user.Role ?? "?");
        }

        using (LogContext.PushProperty("correlation_id", correlationId))
        using (LogContext.PushProperty("request_id", correlationId))
        using (LogContext.PushProperty("subscriber_id", subscriber.SubscriberId == Guid.Empty ? null : subscriber.SubscriberId))
        using (LogContext.PushProperty("company_id", company.CompanyId == Guid.Empty ? null : company.CompanyId))
        using (LogContext.PushProperty("user_id", user.IsAuthenticated ? user.UserId : (Guid?)null))
        {
            await _next(context);
        }
    }

    private static bool IsErpOperationalPath(PathString path)
    {
        if (!path.HasValue) return false;
        var p = path.Value!;
        if (!p.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return false;

        foreach (var prefix in _noCompanyPrefixes)
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

        return true;
    }
}

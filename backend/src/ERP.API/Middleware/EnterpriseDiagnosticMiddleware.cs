using ERP.Application.Common;
using Serilog.Context;

namespace ERP.API.Middleware;

/// <summary>
/// Enriquece logs con contexto enterprise (tenant, company, user, correlation).
/// Emite Warning cuando un endpoint ERP operativo recibe un request autenticado
/// sin company_id en el JWT — útil para detectar handlers que deberían estar
/// company-scoped pero no lo están.
/// </summary>
public sealed partial class EnterpriseDiagnosticMiddleware
{
    // Prefijos de ruta que legítimamente operan sin company_id.
    private static readonly string[] _noCompanyPrefixes =
    [
        "/api/v1/me",
        "/api/v1/access",
        "/api/v1/auth",
        "/api/v1/setup",
        "/api/v1/public",
        "/api/integration", // Integration boundary: contexto de actor externo, sin company_id
        "/api/dev",
        "/api/health",
        "/hangfire",
        "/swagger",
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<EnterpriseDiagnosticMiddleware> _logger;

    public EnterpriseDiagnosticMiddleware(
        RequestDelegate next,
        ILogger<EnterpriseDiagnosticMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentTenant tenant,
        ICurrentCompany company,
        ICurrentUser user
    )
    {
        // Detectar request autenticado a ruta ERP operativa sin company_id.
        if (
            user.IsAuthenticated
            && tenant.TenantId != Guid.Empty
            && !company.HasCompanyContext
            && IsErpOperationalPath(context.Request.Path)
        )
        {
            LogNoCompanyContext(
                context.Request.Path.Value,
                context.Request.Method,
                tenant.TenantId,
                user.UserId,
                user.Role ?? "?"
            );
        }

        // correlation_id/request_id ya quedaron en el LogContext via RequestCorrelationMiddleware (1er middleware del pipeline).
        using (
            LogContext.PushProperty(
                "tenant_id",
                tenant.TenantId == Guid.Empty ? null : tenant.TenantId
            )
        )
        using (
            LogContext.PushProperty(
                "company_id",
                company.CompanyId == Guid.Empty ? null : company.CompanyId
            )
        )
        using (LogContext.PushProperty("user_id", user.IsAuthenticated ? user.UserId : (Guid?)null))
        {
            await _next(context);
        }
    }

    private static bool IsErpOperationalPath(PathString path)
    {
        if (!path.HasValue)
            return false;
        var p = path.Value!;
        if (!p.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var prefix in _noCompanyPrefixes)
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

        return true;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "ERP_NO_COMPANY_CTX | path={Path} method={Method} tenant={TenantId} user={UserId} role={Role}"
    )]
    private partial void LogNoCompanyContext(
        string? path,
        string method,
        Guid tenantId,
        Guid userId,
        string role
    );
}

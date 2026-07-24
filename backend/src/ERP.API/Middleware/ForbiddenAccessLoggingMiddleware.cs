using ERP.Application.Common.Security;
using System.Security.Claims;

namespace ERP.API.Middleware;

/// <summary>
/// Registra intentos de acceso denegado (403/401) con contexto mínimo para auditoría y métricas.
/// </summary>
public sealed partial class ForbiddenAccessLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ForbiddenAccessLoggingMiddleware> _logger;
    private readonly ISecurityMetrics _metrics;

    public ForbiddenAccessLoggingMiddleware(
        RequestDelegate next,
        ILogger<ForbiddenAccessLoggingMiddleware> logger,
        ISecurityMetrics metrics)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode is not (401 or 403))
            return;

        var user = context.User;
        var tenantId = ParseGuid(user.FindFirst("tenant_id")?.Value);
        var companyId = ParseGuid(user.FindFirst("company_id")?.Value);
        var correlationId = RequestCorrelationMiddleware.Resolve(context);
        var endpoint = $"{context.Request.Method} {context.Request.Path.Value}";
        var reason = context.Response.StatusCode == 401 ? "unauthorized" : "forbidden";

        _metrics.RecordPermissionDenied(new SecurityMetricTags(
            TenantId: tenantId,
            CompanyId: companyId,
            Endpoint: endpoint,
            RequestType: reason,
            CorrelationId: correlationId));

        LogAccessDenied(
            context.Response.StatusCode,
            endpoint,
            tenantId?.ToString("D") ?? "none",
            companyId?.ToString("D") ?? "none",
            correlationId,
            context.Connection.RemoteIpAddress?.ToString());
    }

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Access denied status={StatusCode} endpoint={Endpoint} tenantId={TenantId} companyId={CompanyId} correlationId={CorrelationId} remote={RemoteIp}")]
    private partial void LogAccessDenied(int statusCode, string endpoint, string tenantId, string companyId, string correlationId, string? remoteIp);
}

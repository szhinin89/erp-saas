using ERP.Application.Common.Security;
using System.Security.Claims;

namespace ERP.API.Middleware;

/// <summary>
/// Registra intentos de acceso denegado (403/401) con contexto mínimo para auditoría y métricas.
/// </summary>
public sealed class ForbiddenAccessLoggingMiddleware
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
        var subscriberId = ParseGuid(user.FindFirst("subscriber_id")?.Value);
        var companyId = ParseGuid(user.FindFirst("company_id")?.Value);
        var correlationId = context.TraceIdentifier;
        var endpoint = $"{context.Request.Method} {context.Request.Path.Value}";
        var reason = context.Response.StatusCode == 401 ? "unauthorized" : "forbidden";

        _metrics.RecordPermissionDenied(new SecurityMetricTags(
            SubscriberId: subscriberId,
            CompanyId: companyId,
            Endpoint: endpoint,
            RequestType: reason,
            CorrelationId: correlationId));

        _logger.LogWarning(
            "Access denied status={StatusCode} endpoint={Endpoint} subscriberId={SubscriberId} companyId={CompanyId} " +
            "correlationId={CorrelationId} remote={RemoteIp}",
            context.Response.StatusCode,
            endpoint,
            subscriberId?.ToString("D") ?? "none",
            companyId?.ToString("D") ?? "none",
            correlationId,
            context.Connection.RemoteIpAddress?.ToString());
    }

    private static Guid? ParseGuid(string? value)
        => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;
}

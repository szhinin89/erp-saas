using ERP.Application.Common;
using Serilog.Context;

namespace ERP.API.Middleware;

/// <summary>
/// Enriquece logs con contexto enterprise (subscriber, company, user, correlation).
/// </summary>
public sealed class EnterpriseDiagnosticMiddleware
{
    private readonly RequestDelegate _next;

    public EnterpriseDiagnosticMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentSubscriber subscriber,
        ICurrentCompany company,
        ICurrentUser user)
    {
        var correlationId = context.TraceIdentifier;
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        using (LogContext.PushProperty("correlation_id", correlationId))
        using (LogContext.PushProperty("request_id", correlationId))
        using (LogContext.PushProperty("subscriber_id", subscriber.SubscriberId == Guid.Empty ? null : subscriber.SubscriberId))
        using (LogContext.PushProperty("company_id", company.CompanyId == Guid.Empty ? null : company.CompanyId))
        using (LogContext.PushProperty("user_id", user.IsAuthenticated ? user.UserId : (Guid?)null))
        {
            await _next(context);
        }
    }
}

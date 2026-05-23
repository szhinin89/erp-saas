using ERP.Application.Platform.Observability;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.API.Filters;

/// <summary>
/// RFC 8594 deprecation headers + Phase 2 usage telemetry for strangler migration.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DeprecatedApiAttribute : Attribute, IActionFilter
{
    private readonly string? _successor;

    public DeprecatedApiAttribute(string? successor = null) => _successor = successor;

    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var http = context.HttpContext;
        var path = http.Request.Path.Value ?? string.Empty;
        var method = http.Request.Method;

        var headers = http.Response.Headers;
        headers["Deprecation"] = "true";
        headers["X-Api-Deprecated"] = "true";
        headers["X-Deprecated-Endpoint"] = path;
        if (_successor is not null)
            headers["Link"] = $"<{_successor}>; rel=\"successor-version\"";

        var tracker = http.RequestServices.GetService<ILegacyEndpointUsageTracker>();
        tracker?.Record(path, method, _successor, http.Connection.RemoteIpAddress?.ToString());

        var logger = http.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("DeprecatedApi");
        logger?.LogWarning(
            "Deprecated API {Method} {Path} invoked (successor={Successor}, status={Status})",
            method,
            path,
            _successor ?? "none",
            http.Response.StatusCode);
    }
}

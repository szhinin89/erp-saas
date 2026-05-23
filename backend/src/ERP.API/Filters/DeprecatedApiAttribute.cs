using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.API.Filters;

/// <summary>
/// Adds RFC 8594 Deprecation header (and optional Link successor) to HTTP responses.
/// Apply to controllers or actions superseded by /api/platform/* canonical routes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DeprecatedApiAttribute : Attribute, IActionFilter
{
    private readonly string? _successor;

    public DeprecatedApiAttribute(string? successor = null)
    {
        _successor = successor;
    }

    public void OnActionExecuting(ActionExecutingContext context) { }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"] = "true";
        headers["X-Api-Deprecated"] = "true";
        if (_successor is not null)
            headers["Link"] = $"<{_successor}>; rel=\"successor-version\"";
    }
}

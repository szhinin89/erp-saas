using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using ERP.Application.Common.Subscriptions;

namespace ERP.API.Middleware;

/// <summary>
/// Enforces ReadOnly mode for subscribers in GracePeriod or PastDue.
/// Blocks write HTTP methods (POST, PUT, PATCH, DELETE) for read-only tenants.
/// Allows: GET, HEAD, OPTIONS.
/// Bypass: PlatformOperator, SuperAdmin, unauthenticated, exempt paths.
///
/// Place AFTER SubscriptionAccessMiddleware in the pipeline so
/// SubscriptionAccessMode is already evaluated and cached.
/// </summary>
public sealed class ReadOnlyEnforcementMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static readonly HashSet<string> WriteMethods = new(StringComparer.OrdinalIgnoreCase)
        { "POST", "PUT", "PATCH", "DELETE" };

    private readonly RequestDelegate _next;
    private readonly ILogger<ReadOnlyEnforcementMiddleware> _logger;

    public ReadOnlyEnforcementMiddleware(RequestDelegate next, ILogger<ReadOnlyEnforcementMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISubscriptionAccessService accessService)
    {
        // Only block write methods
        if (!WriteMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip unauthenticated / exempt paths
        if (context.User?.Identity?.IsAuthenticated != true ||
            IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Platform bypass
        var role     = context.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var userType = context.User.FindFirstValue("user_type") ?? string.Empty;
        if (IsPlatformUser(role, userType))
        {
            await _next(context);
            return;
        }

        var subscriberClaim = context.User.FindFirstValue("subscriber_id");
        if (!Guid.TryParse(subscriberClaim, out var subscriberId) || subscriberId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        var result = await accessService.EvaluateAsync(subscriberId, context.RequestAborted);

        if (result.AccessMode == SubscriptionAccessMode.ReadOnly)
        {
            _logger.LogInformation(
                "ReadOnlyEnforcement: BLOCKED {Method} {Path} subscriber={SubscriberId} reason={Reason}",
                context.Request.Method, context.Request.Path, subscriberId, result.DenialReason);

            context.Response.StatusCode  = StatusCodes.Status403Forbidden;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code       = "readonly_mode",
                message    = "Tu cuenta está en modo solo lectura. Las operaciones de escritura no están permitidas.",
                reason     = result.DenialReason.ToString(),
                accessMode = result.AccessMode.ToString(),
            }, JsonOpts), context.RequestAborted);

            return;
        }

        await _next(context);
    }

    private static readonly string[] ExemptPrefixes =
    [
        "/api/auth", "/api/platform/auth", "/api/setup", "/api/admin/iam",
        "/health", "/metrics", "/hangfire", "/swagger",
        "/billing/webhooks",   // webhooks must always go through
    ];

    private static bool IsExemptPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        foreach (var prefix in ExemptPrefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsPlatformUser(string role, string userType) =>
        userType.Equals("Platform", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("PlatformOperator", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("Support",          StringComparison.OrdinalIgnoreCase);
}

using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using ERP.Application.Common.Subscriptions;

namespace ERP.API.Middleware;

/// <summary>
/// Validates subscription access on every authenticated, non-exempt request.
/// Returns 403 + structured JSON when a subscriber cannot access the platform.
/// Place AFTER UseAuthentication() and BEFORE UseAuthorization().
/// </summary>
public sealed class SubscriptionAccessMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<SubscriptionAccessMiddleware> _logger;

    public SubscriptionAccessMiddleware(RequestDelegate next, ILogger<SubscriptionAccessMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ISubscriptionAccessService accessService)
    {
        // Skip unauthenticated requests — auth middleware handles 401
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Skip exempt paths (auth, health, hangfire, setup, swagger)
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Platform operators bypass tenant lifecycle check
        var role = context.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (IsPlatformRole(role))
        {
            await _next(context);
            return;
        }

        // Resolve subscriber ID from claim
        var subscriberClaim = context.User.FindFirstValue("subscriber_id");
        if (!Guid.TryParse(subscriberClaim, out var subscriberId) || subscriberId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        var result = await accessService.EvaluateAsync(subscriberId, context.RequestAborted);
        if (result.CanAccess)
        {
            await _next(context);
            return;
        }

        // Subscriber is blocked — return 403 with structured payload
        _logger.LogInformation(
            "SubscriptionAccess denied for subscriber={SubscriberId} code={Code} reason={Reason}",
            subscriberId, result.DenialCode, result.Reason);

        context.Response.StatusCode  = StatusCodes.Status403Forbidden;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        var body = JsonSerializer.Serialize(new
        {
            code    = result.DenialCode,
            message = "Subscription access denied.",
            reason  = result.Reason,
        }, JsonOpts);

        await context.Response.WriteAsync(body, context.RequestAborted);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Exempt path patterns
    // ──────────────────────────────────────────────────────────────────────────

    private static readonly string[] ExemptPrefixes =
    [
        "/api/auth",
        "/api/platform/auth",
        "/api/setup",
        "/api/admin/iam",
        "/health",
        "/metrics",
        "/hangfire",
        "/swagger",
        "/api/debug",
    ];

    private static bool IsExemptPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return ExemptPrefixes.Any(p => value.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPlatformRole(string role) =>
        role.Equals("PlatformOperator", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("Support",          StringComparison.OrdinalIgnoreCase) ||
        role.Equals("BillingAdmin",     StringComparison.OrdinalIgnoreCase) ||
        role.Equals("Auditor",          StringComparison.OrdinalIgnoreCase);
}

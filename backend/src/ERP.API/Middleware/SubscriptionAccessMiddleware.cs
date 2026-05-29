using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using ERP.Application.Common.Subscriptions;
using ERP.Infrastructure.SaaS;

namespace ERP.API.Middleware;

/// <summary>
/// Cache-first subscription access guard for every authenticated request.
///
/// PERFORMANCE:
///   Cache hit  → immediate allow/deny, ~0-1ms, zero DB queries.
///   Cache miss → 1 JOIN query + cache write, ~5-20ms.
///
/// BYPASS rules (FASE 14):
///   - Unauthenticated requests (handled by auth middleware → 401)
///   - Exempt paths: /api/auth, /health, /hangfire, /swagger, /metrics, /api/setup
///   - Platform roles: PlatformOperator, Support, BillingAdmin, Auditor
///   - SuperAdmin users (user_type = Platform)
///
/// ACCESS MODES (FASE 15):
///   FullAccess → no header
///   ReadOnly   → response header X-Subscription-Access-Mode: ReadOnly
///   Blocked    → 403 + structured JSON
///
/// Response body (Blocked):
///   { "code": "subscription_suspended", "reason": "...", "accessMode": "Blocked" }
/// </summary>
public sealed class SubscriptionAccessMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate                       _next;
    private readonly ILogger<SubscriptionAccessMiddleware> _logger;

    public SubscriptionAccessMiddleware(
        RequestDelegate next,
        ILogger<SubscriptionAccessMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISubscriptionAccessService accessService,
        SubscriptionMetrics metrics)
    {
        // 1 — unauthenticated
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // 2 — exempt paths
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // 3 — FASE 14: platform / super-admin bypass
        if (IsPlatformUser(context.User))
        {
            await _next(context);
            return;
        }

        // 4 — resolve subscriber_id claim
        var subscriberClaim = context.User.FindFirstValue("subscriber_id");
        if (!Guid.TryParse(subscriberClaim, out var subscriberId) || subscriberId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        // 5 — evaluate (cache-first inside the service)
        var result = await accessService.EvaluateAsync(subscriberId, context.RequestAborted);

        if (result.AccessMode == SubscriptionAccessMode.Blocked)
        {
            metrics.AccessDeniedTotal.Add(1,
                new KeyValuePair<string, object?>("reason", result.DenialReason.ToString()));

            _logger.LogInformation(
                "subscription-access: BLOCKED subscriber={SubscriberId} code={Code} reason={Reason}",
                subscriberId, result.DenialCode, result.DenialReason);

            context.Response.StatusCode  = StatusCodes.Status403Forbidden;
            context.Response.ContentType = MediaTypeNames.Application.Json;

            var body = JsonSerializer.Serialize(new
            {
                code       = result.DenialCode,
                reason     = result.DenialReason.ToString(),
                accessMode = result.AccessMode.ToString(),
                message    = "Subscription access denied.",
            }, JsonOpts);

            await context.Response.WriteAsync(body, context.RequestAborted);
            return;
        }

        // FASE 15: ReadOnly — allow but signal frontend via response header
        if (result.AccessMode == SubscriptionAccessMode.ReadOnly)
        {
            context.Response.Headers["X-Subscription-Access-Mode"]   = "ReadOnly";
            context.Response.Headers["X-Subscription-Denial-Reason"] = result.DenialReason.ToString();

            _logger.LogDebug(
                "subscription-access: READONLY subscriber={SubscriberId} reason={Reason}",
                subscriberId, result.DenialReason);
        }

        await _next(context);
    }

    // ── Exempt path patterns ──────────────────────────────────────────────────

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
        foreach (var prefix in ExemptPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // ── Platform/SuperAdmin bypass (FASE 14) ──────────────────────────────────

    private static readonly HashSet<string> PlatformRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "PlatformOperator", "Support", "BillingAdmin", "Auditor",
    };

    private static bool IsPlatformUser(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (PlatformRoles.Contains(role)) return true;

        // user_type = Platform → SuperAdmin global
        var userType = user.FindFirstValue("user_type") ?? string.Empty;
        return userType.Equals("Platform", StringComparison.OrdinalIgnoreCase);
    }
}

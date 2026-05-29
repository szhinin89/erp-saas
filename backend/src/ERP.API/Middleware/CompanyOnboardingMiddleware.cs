using System.Net.Mime;
using System.Security.Claims;
using System.Text.Json;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.Company.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ERP.API.Middleware;

/// <summary>
/// Blocks ERP runtime access when the active company has not completed onboarding.
/// Returns HTTP 403 with code "company_onboarding_required" so the frontend can redirect to /onboarding/company.
///
/// BYPASS: Platform roles, SuperAdmin, unauthenticated, exempt paths.
/// GATE: Only applies when JWT has a company_id claim (ERP runtime context).
///
/// Exempt paths (always allowed):
///   /api/auth/*        — login, refresh, logout
///   /api/onboarding/*  — the onboarding wizard API
///   /api/saas/*        — SaaS billing/plan info
///   /api/platform/*    — platform control plane
///   /api/admin/*       — internal admin
///   /health, /metrics, /hangfire, /swagger
/// </summary>
public sealed class CompanyOnboardingMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Short-lived in-memory cache to avoid DB hit on every request for the same company
    private static readonly MemoryCacheEntryOptions CacheOpts = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<CompanyOnboardingMiddleware> _logger;
    private readonly IMemoryCache _cache;

    public CompanyOnboardingMiddleware(
        RequestDelegate next,
        ILogger<CompanyOnboardingMiddleware> logger,
        IMemoryCache cache)
    {
        _next   = next;
        _logger = logger;
        _cache  = cache;
    }

    public async Task InvokeAsync(HttpContext context, ICompanyRepository companyRepo)
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

        // 3 — platform / super-admin bypass
        if (IsPlatformUser(context.User))
        {
            await _next(context);
            return;
        }

        // 4 — only applies when the user has a company context (ERP runtime)
        var companyClaim = context.User.FindFirstValue("company_id");
        if (!Guid.TryParse(companyClaim, out var companyId) || companyId == Guid.Empty)
        {
            await _next(context);
            return;
        }

        // 5 — check onboarding status (cached 30s to avoid DB hit per request)
        var cacheKey = $"onboarding:{companyId}";
        if (!_cache.TryGetValue(cacheKey, out bool onboardingCompleted))
        {
            var company = await companyRepo.GetByIdAsync(companyId, context.RequestAborted);
            onboardingCompleted = company?.OnboardingCompleted ?? true; // null = unknown, allow through
            _cache.Set(cacheKey, onboardingCompleted, CacheOpts);
        }

        if (onboardingCompleted)
        {
            await _next(context);
            return;
        }

        // 6 — block: onboarding not complete
        _logger.LogInformation(
            "CompanyOnboarding: BLOCKED company={CompanyId} path={Path}",
            companyId, context.Request.Path);

        context.Response.StatusCode  = StatusCodes.Status403Forbidden;
        context.Response.ContentType = MediaTypeNames.Application.Json;

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            code    = "company_onboarding_required",
            message = "Company onboarding must be completed before accessing the ERP.",
            redirect = "/onboarding/company",
        }, JsonOpts), context.RequestAborted);
    }

    // ── Exempt paths ──────────────────────────────────────────────────────────

    private static readonly string[] ExemptPrefixes =
    [
        "/api/auth",
        "/api/onboarding",
        "/api/saas",
        "/api/platform",
        "/api/admin",
        "/api/setup",
        "/api/entitlements",    // entitlements needed by SaaS overview
        "/api/companies",       // needed to read company info on login
        "/billing/webhooks",
        "/health",
        "/metrics",
        "/hangfire",
        "/swagger",
    ];

    private static bool IsExemptPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        foreach (var prefix in ExemptPrefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool IsPlatformUser(ClaimsPrincipal user)
    {
        var role     = user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var userType = user.FindFirstValue("user_type") ?? string.Empty;
        return userType.Equals("Platform", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("PlatformOperator", StringComparison.OrdinalIgnoreCase) ||
               role.Equals("Support",          StringComparison.OrdinalIgnoreCase);
    }
}

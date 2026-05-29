using System.Security.Claims;
using ERP.Application.Common.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.API.Filters;

/// <summary>
/// Marks a controller or action as requiring a specific commercial feature/module.
/// Usage: [RequiresCommercialFeature("PAYROLL")]
/// On denial: returns 403 with structured JSON { code, message, feature }.
/// Platform operators bypass this check.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequiresCommercialFeatureAttribute : Attribute, IAsyncResourceFilter
{
    private readonly string _featureCode;

    public RequiresCommercialFeatureAttribute(string featureCode)
    {
        _featureCode = featureCode;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        // Platform operators bypass feature gates
        var role = context.HttpContext.User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (IsPlatformRole(role))
        {
            await next();
            return;
        }

        var subscriberClaim = context.HttpContext.User.FindFirstValue("subscriber_id");
        if (!Guid.TryParse(subscriberClaim, out var subscriberId) || subscriberId == Guid.Empty)
        {
            // Unauthenticated — let auth handle it
            await next();
            return;
        }

        var featureService = context.HttpContext.RequestServices.GetService<IFeatureAccessService>();
        if (featureService is null)
        {
            // Service not registered — fail open (don't break auth)
            await next();
            return;
        }

        var hasFeature = await featureService.HasFeatureAsync(subscriberId, _featureCode, context.HttpContext.RequestAborted);
        if (!hasFeature)
        {
            context.Result = new ObjectResult(new
            {
                code    = "feature_not_included",
                message = $"La funcionalidad '{_featureCode}' no está incluida en tu plan actual.",
                feature = _featureCode,
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
            return;
        }

        await next();
    }

    private static bool IsPlatformRole(string role) =>
        role.Equals("PlatformOperator", StringComparison.OrdinalIgnoreCase) ||
        role.Equals("Support",          StringComparison.OrdinalIgnoreCase);
}

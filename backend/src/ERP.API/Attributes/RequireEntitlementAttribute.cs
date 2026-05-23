using ERP.Application.Common;
using ERP.Application.Subscriptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ERP.API.Attributes;

/// <summary>
/// MVC action filter que verifica que el suscriptor autenticado tenga la feature comercial
/// habilitada en su plan antes de ejecutar el endpoint.
/// Devuelve HTTP 402 (Payment Required) cuando el plan no incluye la feature.
/// Skipped si el request no tiene subscriber_id (SuperAdmin / Platform tokens).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireEntitlementAttribute : Attribute, IAsyncActionFilter
{
    public string FeatureCode { get; }

    public RequireEntitlementAttribute(string featureCode)
    {
        if (string.IsNullOrWhiteSpace(featureCode))
            throw new ArgumentException("FeatureCode requerido.", nameof(featureCode));
        FeatureCode = featureCode.Trim().ToUpperInvariant();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var subscriberIdClaim = context.HttpContext.User.FindFirst("subscriber_id")?.Value;

        // Platform tokens (SuperAdmin) no tienen subscriber_id → bypass
        if (string.IsNullOrEmpty(subscriberIdClaim) || !Guid.TryParse(subscriberIdClaim, out var subscriberId))
        {
            await next();
            return;
        }

        var entitlements = context.HttpContext.RequestServices
            .GetRequiredService<ISubscriberEntitlementsService>();

        var hasFeature = await entitlements.HasFeatureAsync(subscriberId, FeatureCode);
        if (!hasFeature)
        {
            var body = new
            {
                success = false,
                message = $"Tu plan no incluye la funcionalidad '{FeatureCode}'. Actualiza tu plan para acceder.",
                code = "entitlement_required",
                featureCode = FeatureCode,
            };
            context.Result = new ObjectResult(body) { StatusCode = StatusCodes.Status402PaymentRequired };
            return;
        }

        await next();
    }
}

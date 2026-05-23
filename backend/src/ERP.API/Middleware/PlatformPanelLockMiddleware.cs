using ERP.API.Contracts;
using ERP.Application.Common;
using System.Net.Mime;

namespace ERP.API.Middleware;

/// <summary>
/// Cuando <c>Deployment:PlatformPanelEnabled</c> es false, bloquea login platform y sesiones con rol
/// <see cref="PlatformAuthConstants.JwtPlatformOperatorRole"/> para instancias solo tenant.
/// </summary>
public sealed class PlatformPanelLockMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IDeploymentFeatureFlags deployment)
    {
        if (deployment.IsPlatformPanelEnabled)
        {
            await next(context);
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        if (HttpMethods.IsGet(context.Request.Method) &&
            (path.Equals("/api/public/deployment", StringComparison.OrdinalIgnoreCase) ||
             path.Equals("/api/public/plans", StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsPost(context.Request.Method) &&
            path.Equals("/api/platform/auth/login", StringComparison.OrdinalIgnoreCase))
        {
            await WriteForbiddenAsync(context);
            return;
        }

        if (context.User?.Identity?.IsAuthenticated == true &&
            context.User.IsInRole(PlatformAuthConstants.JwtPlatformOperatorRole))
        {
            await WriteForbiddenAsync(context);
            return;
        }

        await next(context);
    }

    private static async Task WriteForbiddenAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = MediaTypeNames.Application.Json;
        var body = new ApiResponse<object>(
            Success: false,
            Message: DeploymentAuthMessages.PlatformPanelDisabledUserMessage,
            ResponseObject: new { });
        await context.Response.WriteAsJsonAsync(body);
    }
}

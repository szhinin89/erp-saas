using Serilog.Context;

namespace ERP.API.Middleware;

/// <summary>
/// Única fuente del <c>correlationId</c> por request. Si el request llega con el
/// header <see cref="HeaderName"/> (gateway/cliente confiable) lo reutiliza; si no,
/// usa <see cref="HttpContext.TraceIdentifier"/>. El valor resuelto se expone en
/// <see cref="HttpContext.Items"/> (vía <see cref="Resolve"/>), en el contexto de
/// Serilog y en el header de respuesta.
///
/// Debe registrarse PRIMERO en el pipeline (antes de <c>ExceptionMiddleware</c>)
/// para que incluso un error temprano tenga <c>correlationId</c> consistente.
/// Ningún otro componente (ResponseFactory, controllers, handlers,
/// ExceptionMiddleware) debe generar su propio id: todos deben llamar a
/// <see cref="Resolve"/>.
/// </summary>
public sealed class RequestCorrelationMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemsKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public RequestCorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Devuelve el <c>correlationId</c> resuelto para este request. Si el middleware
    /// no se ejecutó (p. ej. <c>HttpContext</c> construido a mano en tests unitarios),
    /// cae a <see cref="HttpContext.TraceIdentifier"/>.
    /// </summary>
    public static string Resolve(HttpContext context)
        => context.Items.TryGetValue(ItemsKey, out var value) && value is string id
            ? id
            : context.TraceIdentifier;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(incoming) ? context.TraceIdentifier : incoming;

        context.Items[ItemsKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("correlation_id", correlationId))
        using (LogContext.PushProperty("request_id", correlationId))
        {
            await _next(context);
        }
    }
}

/// <summary>Extension methods para registrar <see cref="RequestCorrelationMiddleware"/> en el pipeline.</summary>
public static class RequestCorrelationMiddlewareExtensions
{
    /// <summary>Registra <see cref="RequestCorrelationMiddleware"/>. Debe ser el primer middleware del pipeline.</summary>
    public static IApplicationBuilder UseRequestCorrelation(this IApplicationBuilder app)
        => app.UseMiddleware<RequestCorrelationMiddleware>();
}

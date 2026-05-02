using System.Net;
using System.Text.Json;
using ERP.Domain.Subscriptions.Exceptions;

namespace ERP.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Logueamos el detalle completo internamente; el cliente recibe solo un mensaje seguro.
            _logger.LogError(ex, "Error no controlado en {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Mensajes de ArgumentException / InvalidOperationException suelen ser textos de validación
        // pensados para el usuario (dominio y aplicación). Mostrarlos mejora la claridad en la UI;
        // el detalle técnico sigue solo en logs.
        var (statusCode, message) = exception switch
        {
            ArgumentException arg =>
                (HttpStatusCode.BadRequest,
                 string.IsNullOrWhiteSpace(arg.Message) ? "Solicitud inválida." : arg.Message.Trim()),
            InvalidOperationException inv =>
                (HttpStatusCode.UnprocessableEntity,
                 string.IsNullOrWhiteSpace(inv.Message) ? "No se puede completar la operación." : inv.Message.Trim()),
            FeatureNotEntitledException feat =>
                (HttpStatusCode.Forbidden,
                 string.IsNullOrWhiteSpace(feat.Message) ? "Funcionalidad no incluida en el plan." : feat.Message.Trim()),
            SubscriptionLimitExceededException lim =>
                (HttpStatusCode.Conflict,
                 string.IsNullOrWhiteSpace(lim.Message) ? "Límite de suscripción alcanzado." : lim.Message.Trim()),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado."),
            _ => (HttpStatusCode.InternalServerError, "Error interno del servidor."),
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new { status = context.Response.StatusCode, message };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

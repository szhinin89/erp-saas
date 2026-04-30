using System.Net;
using System.Text.Json;

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

        var (statusCode, message) = exception switch
        {
            ArgumentException           => (HttpStatusCode.BadRequest,            "Solicitud inválida."),
            InvalidOperationException   => (HttpStatusCode.UnprocessableEntity,   "No se puede completar la operación."),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,           "No autorizado."),
            _                          => (HttpStatusCode.InternalServerError,    "Error interno del servidor.")
        };

        context.Response.StatusCode = (int)statusCode;

        var response = new { status = context.Response.StatusCode, message };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

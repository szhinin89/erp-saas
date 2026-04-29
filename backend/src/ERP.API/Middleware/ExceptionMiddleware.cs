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
            _logger.LogError(ex, "Error no controlado: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode  = exception switch
        {
            ArgumentException        => (int)HttpStatusCode.BadRequest,
            InvalidOperationException => (int)HttpStatusCode.UnprocessableEntity,
            UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
            _                        => (int)HttpStatusCode.InternalServerError
        };

        var response = new
        {
            status  = context.Response.StatusCode,
            message = exception.Message
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

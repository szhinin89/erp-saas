using System.Net;
using System.Text.Json;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ERP.API.Middleware;

public partial class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IWebHostEnvironment environment)
    {
        _next        = next;
        _logger      = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (IsClientDisconnectedCancellation(context, ex))
        {
            // Cliente cerró la pestaña, navegó fuera o Axios abortó la petición; no es fallo del servidor.
            LogRequestCancelled(ex, context.Request.Method, context.Request.Path);
            throw;
        }
        catch (ValidationException ex)
        {
            // ValidationException es una condición esperada — no es un error del servidor.
            LogValidationError(ex.Errors.Count(), context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex, _environment);
        }
        catch (Exception ex)
        {
            // Errores reales no controlados: loguear con nivel Error para alertas.
            LogUnhandledError(ex, context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex, _environment);
        }
    }

    private static bool IsClientDisconnectedCancellation(HttpContext context, Exception ex)
    {
        if (!context.RequestAborted.IsCancellationRequested)
            return false;

        return ex is OperationCanceledException or TaskCanceledException;
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception, IWebHostEnvironment environment)
    {
        // Si la respuesta ya se inició (headers enviados), no podemos cambiar StatusCode ni ContentType.
        // Solo registrar en el log y abortar — el cliente recibirá lo que ya se envió.
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";

        var (statusCode, code) = exception switch
        {
            ValidationException =>
                (HttpStatusCode.UnprocessableEntity, ApiResponseCodes.Common.ValidationError),
            // FASE 7: Optimistic concurrency violation → 409 Conflict
            DbUpdateConcurrencyException =>
                (HttpStatusCode.Conflict, ApiResponseCodes.Common.ConcurrencyConflict),
            DbUpdateException =>
                (HttpStatusCode.ServiceUnavailable, ApiResponseCodes.Common.DatabaseUnavailable),
            ArgumentException =>
                (HttpStatusCode.BadRequest, ApiResponseCodes.Common.BadRequest),
            InvalidOperationException =>
                (HttpStatusCode.UnprocessableEntity, ApiResponseCodes.Common.DomainRuleViolation),
            SriCommunicationException =>
                (HttpStatusCode.BadGateway, ApiResponseCodes.Common.SriCommunicationError),
            ERP.Domain.Exceptions.CompanyScopeException =>
                (HttpStatusCode.Forbidden, ApiResponseCodes.Common.CompanyScopeForbidden),
            ERP.Domain.Exceptions.BranchScopeException =>
                (HttpStatusCode.Forbidden, ApiResponseCodes.Common.BranchScopeForbidden),
            CompanyRucAlreadyExistsException =>
                (HttpStatusCode.Conflict, ApiResponseCodes.Common.CompanyRucAlreadyExists),
            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, ApiResponseCodes.Common.Unauthorized),
            _ => (HttpStatusCode.InternalServerError, ApiResponseCodes.Common.InternalError),
        };

        context.Response.StatusCode = (int)statusCode;

        // Detalle dinámico de la instancia → data.errors.
        // ValidationException: mapa { campo: [mensajes] } (camelCase).
        // Otras excepciones: lista plana de strings.
        ApiResponse<object> response;
        if (exception is ValidationException validationException)
        {
            var fieldErrors = validationException.Errors
                .Where(e => e is not null)
                .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName)
                    ? "_"
                    : ToCamelCase(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            response = ResponseFactory.ValidationError(context, environment, code, fieldErrors);
        }
        else
        {
            var errors = exception switch
            {
                ArgumentException or InvalidOperationException or SriCommunicationException
                    or ERP.Domain.Exceptions.CompanyScopeException or ERP.Domain.Exceptions.BranchScopeException
                    or CompanyRucAlreadyExistsException
                    when !string.IsNullOrWhiteSpace(exception.Message) => new[] { exception.Message.Trim() },
                _ => Array.Empty<string>(),
            };
            response = ResponseFactory.Error(context, environment, code, errors);
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static string ToCamelCase(string name)
        => name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    [LoggerMessage(Level = LogLevel.Debug, Message = "Petición cancelada (cliente desconectado): {Method} {Path}")]
    private partial void LogRequestCancelled(Exception ex, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validación fallida ({FailureCount} error(es)): {Method} {Path}")]
    private partial void LogValidationError(int failureCount, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error no controlado en {Method} {Path}")]
    private partial void LogUnhandledError(Exception ex, string method, PathString path);
}

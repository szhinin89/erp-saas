using System.Net;
using System.Text.Json;
using FluentValidation;
using ERP.Application.Common.Exceptions;
using ERP.Domain.Exceptions;
using ERP.Domain.Subscribers.Exceptions;
using ERP.Domain.Subscriptions.Exceptions;
using Microsoft.EntityFrameworkCore;

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
        catch (Exception ex) when (IsClientDisconnectedCancellation(context, ex))
        {
            // Cliente cerró la pestaña, navegó fuera o Axios abortó la petición; no es fallo del servidor.
            _logger.LogDebug(ex, "Petición cancelada (cliente desconectado): {Method} {Path}",
                context.Request.Method, context.Request.Path);
            throw;
        }
        catch (Exception ex)
        {
            // Logueamos el line completo internamente; el cliente recibe solo un mensaje seguro.
            _logger.LogError(ex, "Error no controlado en {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static bool IsClientDisconnectedCancellation(HttpContext context, Exception ex)
    {
        if (!context.RequestAborted.IsCancellationRequested)
            return false;

        return ex is OperationCanceledException or TaskCanceledException;
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Mensajes de ArgumentException / InvalidOperationException suelen ser textos de validación
        // pensados para el usuario (dominio y aplicación). Mostrarlos mejora la claridad en la UI;
        // el line técnico sigue solo en logs.
        var validationErrors = exception is ValidationException validationException
            ? validationException.Errors
                .Where(e => e is not null)
                .Select(e => new
                {
                    field = string.IsNullOrWhiteSpace(e.PropertyName) ? null : e.PropertyName,
                    message = e.ErrorMessage
                })
                .ToArray()
            : Array.Empty<object>();

        var (statusCode, message) = exception switch
        {
            ValidationException =>
                (HttpStatusCode.UnprocessableEntity, "La solicitud contiene errores de validación."),
            // FASE 12: Invalid lifecycle transitions → 409 Conflict
            InvalidLifecycleTransitionException lifecycleTx =>
                (HttpStatusCode.Conflict,
                 string.IsNullOrWhiteSpace(lifecycleTx.Message)
                     ? "Transición de estado no permitida."
                     : lifecycleTx.Message.Trim()),
            // FASE 7: Optimistic concurrency violation → 409 Conflict
            DbUpdateConcurrencyException =>
                (HttpStatusCode.Conflict,
                 "El recurso fue modificado por otro proceso. Reintente la operación."),
            ArgumentException arg =>
                (HttpStatusCode.BadRequest,
                 string.IsNullOrWhiteSpace(arg.Message) ? "Solicitud inválida." : arg.Message.Trim()),
            InvalidOperationException inv =>
                (HttpStatusCode.UnprocessableEntity,
                 string.IsNullOrWhiteSpace(inv.Message) ? "No se puede completar la operación." : inv.Message.Trim()),
            SriCommunicationException sri =>
                (HttpStatusCode.BadGateway,
                 string.IsNullOrWhiteSpace(sri.Message)
                     ? "Error de comunicación con el SRI. La salesBill quedó en estado ErrorEnvio para reintentar."
                     : $"Error SRI: {sri.Message.Trim()}"),
            FeatureNotEntitledException feat =>
                (HttpStatusCode.Forbidden,
                 string.IsNullOrWhiteSpace(feat.Message) ? "Funcionalidad no incluida en el plan." : feat.Message.Trim()),
            SubscriptionLimitExceededException lim =>
                (HttpStatusCode.Conflict,
                 string.IsNullOrWhiteSpace(lim.Message) ? "Límite de suscripción alcanzado." : lim.Message.Trim()),
            CommercialPlanLimitExceededException planLim =>
                (HttpStatusCode.Forbidden,
                 string.IsNullOrWhiteSpace(planLim.Message)
                     ? "Commercial plan limit reached."
                     : planLim.Message.Trim()),
            ERP.Domain.Billing.Exceptions.BillingAccessDeniedException billingDenied =>
                (HttpStatusCode.Forbidden,
                 string.IsNullOrWhiteSpace(billingDenied.Message)
                     ? "Acceso restringido por estado de facturación."
                     : billingDenied.Message.Trim()),
            ERP.Domain.Exceptions.CompanyScopeException companyScope =>
                (HttpStatusCode.Forbidden,
                 string.IsNullOrWhiteSpace(companyScope.Message)
                     ? "Acceso denegado por contexto de empresa."
                     : companyScope.Message.Trim()),
            CompanyRucAlreadyExistsException rucExists =>
                (HttpStatusCode.Conflict, rucExists.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "No autorizado."),
            _ => (HttpStatusCode.InternalServerError, "Error interno del servidor."),
        };

        context.Response.StatusCode = (int)statusCode;

        object response = exception switch
        {
            ValidationException => validationErrors.Length > 0
                ? new { status = context.Response.StatusCode, message, errors = validationErrors }
                : new { status = context.Response.StatusCode, message },
            CompanyRucAlreadyExistsException rucExists => new
            {
                status = context.Response.StatusCode,
                message,
                code = CompanyRucAlreadyExistsException.ErrorCode,
                taxId = rucExists.TaxId,
            },
            _ => validationErrors.Length > 0
                ? new { status = context.Response.StatusCode, message, errors = validationErrors }
                : new { status = context.Response.StatusCode, message },
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}

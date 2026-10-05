using System.Reflection;
using System.Text.Json;
using ERP.API.Contracts;
using ERP.API.Extensions;
using ERP.Application.Common;
using ERP.Application.Common.Exceptions;
using ERP.Application.Common.Persistence;
using ERP.Domain.Exceptions;
using FluentValidation;

namespace ERP.API.Middleware;

public partial class ExceptionMiddleware
{
    // ZH-TEMPORAL-CONTRACT-02J: mismo contrato de instante que MVC (Program.cs) — "…Z".
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new ERP.API.Temporal.UtcInstantJsonConverter() },
    };

    /// <summary>
    /// Capas cuyo texto de <see cref="ArgumentException"/> es un mensaje de validación curado (Domain y
    /// Application). Lanzada desde cualquier otro código (framework, Infrastructure, API) su texto
    /// puede nombrar parámetros o configuración: conserva el 400 pero no se expone. Las reglas de
    /// negocio NO dependen de esto: viajan como <see cref="DomainRuleViolationException"/>.
    /// </summary>
    private static readonly Assembly[] CuratedMessageAssemblies =
    [
        typeof(CompanyScopeException).Assembly, // ERP.Domain
        typeof(ApiResponseCodes).Assembly, // ERP.Application
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IDatabaseExceptionTranslator _databaseExceptions;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IWebHostEnvironment environment,
        IDatabaseExceptionTranslator databaseExceptions
    )
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _databaseExceptions = databaseExceptions;
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
            await HandleExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            // Errores reales no controlados: loguear con nivel Error para alertas. El detalle
            // técnico completo (SQL, conexión, stack, inner exceptions) queda SOLO aquí.
            LogUnhandledError(ex, context.Request.Method, context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static bool IsClientDisconnectedCancellation(HttpContext context, Exception ex)
    {
        if (!context.RequestAborted.IsCancellationRequested)
            return false;

        return ex is OperationCanceledException or TaskCanceledException;
    }

    /// <summary>
    /// Clasificación única excepción → código (ZH-BACKEND-SECURITY-ERROR-FINAL-HARDENING-01). El
    /// HTTP status sale de <see cref="ApiErrorStatus"/>; el texto solo viaja si <see cref="ExposesMessage"/>.
    /// </summary>
    internal string Classify(Exception exception) =>
        exception switch
        {
            ValidationException => ApiResponseCodes.Common.ValidationError,
            // ZH-DATETIME-UTC-GUARDRAILS-01: violación de invariante (DateTime sin normalizar a
            // UTC) detectada por UtcDateTimeGuardInterceptor antes de tocar la base de datos — no
            // es una caída/timeout de PostgreSQL, nunca debe salir como DATABASE_UNAVAILABLE.
            UnspecifiedDateTimeKindException => ApiResponseCodes.Common.InvalidDateTimeKind,
            // Excepciones semánticas antes que la clasificación técnica: CompanyRucAlreadyExists
            // puede envolver la violación UNIQUE que la originó.
            SriCommunicationException => ApiResponseCodes.Common.SriCommunicationError,
            CompanyScopeException => ApiResponseCodes.Common.CompanyScopeForbidden,
            BranchScopeException => ApiResponseCodes.Common.BranchScopeForbidden,
            CompanyRucAlreadyExistsException => ApiResponseCodes.Common.CompanyRucAlreadyExists,
            UnauthorizedAccessException => ApiResponseCodes.Common.Unauthorized,
            // ZH-DOMAIN-RULE-ERROR-SSOT-01: la única representación de una regla de negocio. Llega aquí
            // solo si no pasó por DomainRuleBehavior (request cuya respuesta no es Result<T>, o código
            // fuera de MediatR): mismo código y mensaje que Result.FromDomainRule.
            IApiCodedDomainRule coded => coded.ApiCode,
            DomainRuleViolationException => ApiResponseCodes.Common.DomainRuleViolation,
            // Base de datos: único punto de clasificación técnica (IDatabaseExceptionTranslator):
            // no disponible → 503; UNIQUE/integridad/concurrencia → 409; SQL inesperado → 500.
            _ when _databaseExceptions.ClassifyFailureCode(exception) is { } databaseCode =>
                databaseCode,
            ArgumentException => ApiResponseCodes.Common.BadRequest,
            // InvalidOperationException = error interno / estado imposible / framework → 500 sin texto
            // (cae aquí junto con cualquier otra excepción no clasificada).
            _ => ApiResponseCodes.Common.InternalError,
        };

    /// <summary>¿El texto de la excepción puede ir en <c>data.errors</c>? Solo si es un mensaje curado.</summary>
    internal static bool ExposesMessage(Exception exception, string code) =>
        !string.IsNullOrWhiteSpace(exception.Message)
        && ApiErrorStatus.ExposesDetail(code)
        && exception switch
        {
            CompanyScopeException
            or BranchScopeException
            or CompanyRucAlreadyExistsException
            or SriCommunicationException
            or DomainRuleViolationException => true,
            ArgumentException => IsCuratedMessage(exception),
            _ => false,
        };

    /// <summary>Origen del <c>throw</c> de un ArgumentException (primer frame): Domain o Application.</summary>
    private static bool IsCuratedMessage(Exception exception) =>
        exception.TargetSite?.DeclaringType?.Assembly is { } origin
        && CuratedMessageAssemblies.Contains(origin);

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var environment = _environment;
        // Si la respuesta ya se inició (headers enviados), no podemos cambiar StatusCode ni ContentType.
        // Solo registrar en el log y abortar — el cliente recibirá lo que ya se envió.
        if (context.Response.HasStarted)
            return;

        context.Response.ContentType = "application/json";

        // Este middleware solo decide el `code` de cada excepción; el HTTP status sale de la misma
        // tabla única que usan los fallos de Result<T> (ApiErrorStatus, ADR-027 §9).
        var code = Classify(exception);

        context.Response.StatusCode = ApiErrorStatus.For(code);

        // Detalle dinámico de la instancia → data.errors.
        // ValidationException: mapa { campo: [mensajes] } (camelCase).
        // Otras excepciones: lista plana de strings.
        ApiResponse<object> response;
        if (exception is ValidationException validationException)
        {
            var fieldErrors = validationException
                .Errors.Where(e => e is not null)
                .GroupBy(e =>
                    string.IsNullOrWhiteSpace(e.PropertyName) ? "_" : ToCamelCase(e.PropertyName)
                )
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            response = ResponseFactory.ValidationError(context, environment, code, fieldErrors);
        }
        else
        {
            var errors = ExposesMessage(exception, code)
                ? new[] { exception.Message.Trim() }
                : Array.Empty<string>();
            response = ResponseFactory.Error(context, environment, code, errors);
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static string ToCamelCase(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Petición cancelada (cliente desconectado): {Method} {Path}"
    )]
    private partial void LogRequestCancelled(Exception ex, string method, PathString path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Validación fallida ({FailureCount} error(es)): {Method} {Path}"
    )]
    private partial void LogValidationError(int failureCount, string method, PathString path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error no controlado en {Method} {Path}")]
    private partial void LogUnhandledError(Exception ex, string method, PathString path);
}

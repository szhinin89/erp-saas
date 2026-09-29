using ERP.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Extensions;

/// <summary>
/// Helpers para que los controllers devuelvan <c>ApiResponse&lt;T&gt;</c> a través de
/// <see cref="ResponseFactory"/>. <c>code</c> (de <see cref="ApiResponseCodes"/>) es la
/// única fuente de verdad de <c>severity</c> y de los mensajes — nadie debe construir
/// el envelope a mano (regla B-03). El HTTP status de cualquier error sale de
/// <see cref="ApiErrorStatus"/> (tabla única código → categoría → HTTP): ningún helper
/// decide su propio status.
/// </summary>
public static class ApiResultExtensions
{
    private static IWebHostEnvironment Env(ControllerBase controller) =>
        controller.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

    public static IActionResult ApiOk<T>(
        this ControllerBase controller,
        T payload,
        string code = ApiResponseCodes.Common.Ok
    ) =>
        controller.Ok(
            ResponseFactory.Success(controller.HttpContext, Env(controller), code, payload)
        );

    public static IActionResult ApiCreated<T>(
        this ControllerBase controller,
        T payload,
        string code = ApiResponseCodes.Common.Created
    ) =>
        controller.StatusCode(
            StatusCodes.Status201Created,
            ResponseFactory.Success(controller.HttpContext, Env(controller), code, payload)
        );

    public static IActionResult ApiBadRequest(
        this ControllerBase controller,
        string message = "Solicitud inválida."
    ) => ApiError(controller, ApiResponseCodes.Common.BadRequest, message);

    public static IActionResult ApiUnauthorized(
        this ControllerBase controller,
        string message = "No autorizado."
    ) => ApiError(controller, ApiResponseCodes.Common.Unauthorized, message);

    public static IActionResult ApiForbidden(
        this ControllerBase controller,
        string message = "Forbidden"
    ) => ApiError(controller, ApiResponseCodes.Common.Forbidden, message);

    public static IActionResult ApiNotFound(
        this ControllerBase controller,
        string message = "No encontrado"
    ) => ApiError(controller, ApiResponseCodes.Common.NotFound, message);

    public static IActionResult ApiUnprocessableEntity(
        this ControllerBase controller,
        string message = "No se puede completar la operación."
    ) => ApiError(controller, ApiResponseCodes.Common.ValidationError, message);

    public static IActionResult ToCreatedOrBadRequest<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Created,
        Func<T>? successFallbackFactory = null
    )
    {
        return result.IsSuccess
            ? controller.StatusCode(
                StatusCodes.Status201Created,
                ResponseFactory.Success(
                    controller.HttpContext,
                    Env(controller),
                    result.Code ?? code,
                    ResolveValue(result, successFallbackFactory)
                )
            )
            : controller.ApiFailure(result);
    }

    public static IActionResult ToOkOrBadRequest<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Ok,
        Func<T>? successFallbackFactory = null
    )
    {
        return result.IsSuccess
            ? controller.Ok(
                ResponseFactory.Success(
                    controller.HttpContext,
                    Env(controller),
                    result.Code ?? code,
                    ResolveValue(result, successFallbackFactory)
                )
            )
            : controller.ApiFailure(result);
    }

    /// <summary>
    /// Traducción única de un fallo de <see cref="Result{T}"/> a HTTP: <c>code</c> →
    /// <see cref="ApiErrorStatus"/>, <c>Error</c> → <c>data.errors</c>. Un fallo SIN <c>Code</c>
    /// usa <paramref name="uncodedFallbackCode"/> — el único punto que el endpoint decide, y solo
    /// para fallos que el handler no clasificó (BAD_REQUEST por defecto; NOT_FOUND en lecturas de
    /// recurso para no revelar existencia; UNAUTHORIZED en autenticación). Para endpoints cuyo
    /// éxito no es un envelope (archivos, cookies) y solo necesitan la rama de fallo.
    /// </summary>
    public static IActionResult ApiFailure<T>(
        this ControllerBase controller,
        Result<T> result,
        string uncodedFallbackCode = ApiResponseCodes.Common.BadRequest
    )
    {
        var errors = string.IsNullOrWhiteSpace(result.Error) ? null : new[] { result.Error };
        return ApiError(controller, result.Code ?? uncodedFallbackCode, errors);
    }

    /// <summary>
    /// Lectura de un recurso: éxito → 200. Un fallo CON <c>Code</c> se traduce con la tabla única
    /// (<see cref="ApiErrorStatus"/>, la misma de ToOkOrBadRequest/ToCreatedOrBadRequest): NOT_FOUND →
    /// 404, FORBIDDEN → 403, VALIDATION_ERROR → 422, CONFLICT → 409, etc. Un fallo SIN <c>Code</c>
    /// conserva su contrato histórico de 404: los handlers de "obtener por id" que devuelven
    /// <c>Failure("X no encontrado")</c>, y los que ocultan a propósito la existencia de un recurso
    /// ajeno (p. ej. GetCompanyById sin membresía). ZH-API-RESULT-STATUS-MAPPING-01: antes cualquier
    /// fallo, con o sin código, salía como 404.
    /// </summary>
    public static IActionResult ToOkOrNotFound<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Ok,
        Func<T>? successFallbackFactory = null
    )
    {
        if (result.IsSuccess)
            return controller.Ok(
                ResponseFactory.Success(
                    controller.HttpContext,
                    Env(controller),
                    result.Code ?? code,
                    ResolveValue(result, successFallbackFactory)
                )
            );

        return controller.ApiFailure(result, ApiResponseCodes.Common.NotFound);
    }

    /// <summary>
    /// Contenido binario de un recurso (logo, archivo): éxito → <paramref name="toFile"/>; fallo →
    /// misma regla que <see cref="ToOkOrNotFound{T}"/> (con Code: tabla única; sin Code: 404).
    /// </summary>
    public static IActionResult ToFileOrNotFound<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, IActionResult> toFile
    ) =>
        result.IsSuccess
            ? toFile(result.Value!)
            : controller.ApiFailure(result, ApiResponseCodes.Common.NotFound);

    private static IActionResult ApiError(
        ControllerBase controller,
        string code,
        params string[]? errors
    )
    {
        var response = ResponseFactory.Error(controller.HttpContext, Env(controller), code, errors);

        // El status lo decide solo ApiErrorStatus; aquí únicamente se elige el tipo MVC tipado
        // equivalente (BadRequestObjectResult, NotFoundObjectResult…) que ya exponía la API.
        return ApiErrorStatus.For(code) switch
        {
            StatusCodes.Status400BadRequest => controller.BadRequest(response),
            StatusCodes.Status401Unauthorized => controller.Unauthorized(response),
            StatusCodes.Status404NotFound => controller.NotFound(response),
            StatusCodes.Status409Conflict => controller.Conflict(response),
            StatusCodes.Status422UnprocessableEntity => controller.UnprocessableEntity(response),
            var status => controller.StatusCode(status, response),
        };
    }

    private static T ResolveValue<T>(Result<T> result, Func<T>? successFallbackFactory)
    {
        if (result.Value is not null)
            return result.Value;

        if (successFallbackFactory is not null)
            return successFallbackFactory();

        return default!;
    }
}

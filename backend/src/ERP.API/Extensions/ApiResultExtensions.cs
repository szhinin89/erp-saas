using ERP.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Extensions;

/// <summary>
/// Helpers para que los controllers devuelvan <c>ApiResponse&lt;T&gt;</c> a través de
/// <see cref="ResponseFactory"/>. <c>code</c> (de <see cref="ApiResponseCodes"/>) es la
/// única fuente de verdad de <c>severity</c> y de los mensajes — nadie debe construir
/// el envelope a mano (regla B-03).
/// </summary>
public static class ApiResultExtensions
{
    private static IWebHostEnvironment Env(ControllerBase controller)
        => controller.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

    public static IActionResult ApiOk<T>(
        this ControllerBase controller,
        T payload,
        string code = ApiResponseCodes.Common.Ok)
        => controller.Ok(ResponseFactory.Success(controller.HttpContext, Env(controller), code, payload));

    public static IActionResult ApiCreated<T>(
        this ControllerBase controller,
        T payload,
        string code = ApiResponseCodes.Common.Created)
        => controller.StatusCode(StatusCodes.Status201Created, ResponseFactory.Success(controller.HttpContext, Env(controller), code, payload));

    public static IActionResult ApiBadRequest(
        this ControllerBase controller,
        string message = "Solicitud inválida.")
        => controller.BadRequest(ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.BadRequest, new[] { message }));

    public static IActionResult ApiUnauthorized(
        this ControllerBase controller,
        string message = "No autorizado.")
        => controller.Unauthorized(ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.Unauthorized, new[] { message }));

    public static IActionResult ApiForbidden(
        this ControllerBase controller,
        string message = "Forbidden")
        => controller.StatusCode(StatusCodes.Status403Forbidden, ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.Forbidden, new[] { message }));

    public static IActionResult ApiNotFound(
        this ControllerBase controller,
        string message = "No encontrado")
        => controller.NotFound(ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.NotFound, new[] { message }));

    public static IActionResult ApiUnprocessableEntity(
        this ControllerBase controller,
        string message = "No se puede completar la operación.")
        => controller.UnprocessableEntity(ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.ValidationError, new[] { message }));

    public static IActionResult ApiConflict(
        this ControllerBase controller,
        string message,
        string? code = null)
        => controller.Conflict(ResponseFactory.Error(controller.HttpContext, Env(controller), code ?? ApiResponseCodes.Common.Conflict, new[] { message }));

    public static IActionResult ApiTooManyRequests(
        this ControllerBase controller,
        string message)
        => controller.StatusCode(StatusCodes.Status429TooManyRequests, ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.RateLimited, new[] { message }));

    public static IActionResult ToCreatedOrBadRequest<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Created,
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.StatusCode(StatusCodes.Status201Created, ResponseFactory.Success(controller.HttpContext, Env(controller), result.Code ?? code, ResolveValue(result, successFallbackFactory)))
            : MapFailure(controller, result);
    }

    public static IActionResult ToOkOrBadRequest<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Ok,
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.Ok(ResponseFactory.Success(controller.HttpContext, Env(controller), result.Code ?? code, ResolveValue(result, successFallbackFactory)))
            : MapFailure(controller, result);
    }

    private static IActionResult MapFailure<T>(ControllerBase controller, Result<T> result)
    {
        var code = result.Code ?? ApiResponseCodes.Common.BadRequest;
        var errors = string.IsNullOrWhiteSpace(result.Error) ? null : new[] { result.Error };
        var response = ResponseFactory.Error(controller.HttpContext, Env(controller), code, errors);

        return code switch
        {
            ApiResponseCodes.Common.UniqueViolation or ApiResponseCodes.Common.Conflict or ApiResponseCodes.Common.CompanyRucAlreadyExists
                => controller.Conflict(response),
            ApiResponseCodes.Common.ValidationError => controller.UnprocessableEntity(response),
            ApiResponseCodes.Common.NotFound => controller.NotFound(response),
            ApiResponseCodes.Common.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, response),
            ApiResponseCodes.Common.Unauthorized => controller.Unauthorized(response),
            _ => controller.BadRequest(response),
        };
    }

    public static IActionResult ToOkOrUnauthorized<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Ok,
        Func<T>? successFallbackFactory = null)
    {
        if (result.IsSuccess)
            return controller.Ok(ResponseFactory.Success(controller.HttpContext, Env(controller), result.Code ?? code, ResolveValue(result, successFallbackFactory)));

        var errors = string.IsNullOrWhiteSpace(result.Error) ? null : new[] { result.Error };
        return controller.Unauthorized(ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.Unauthorized, errors));
    }

    public static IActionResult ToOkOrNotFound<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Ok,
        Func<T>? successFallbackFactory = null)
    {
        if (result.IsSuccess)
            return controller.Ok(ResponseFactory.Success(controller.HttpContext, Env(controller), result.Code ?? code, ResolveValue(result, successFallbackFactory)));

        var errors = string.IsNullOrWhiteSpace(result.Error) ? null : new[] { result.Error };
        return controller.NotFound(ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.NotFound, errors));
    }

    public static IActionResult ToOkOrInternalServerError<T>(
        this ControllerBase controller,
        Result<T> result,
        string code = ApiResponseCodes.Common.Ok,
        Func<T>? successFallbackFactory = null)
    {
        if (result.IsSuccess)
            return controller.Ok(ResponseFactory.Success(controller.HttpContext, Env(controller), result.Code ?? code, ResolveValue(result, successFallbackFactory)));

        var errors = string.IsNullOrWhiteSpace(result.Error) ? null : new[] { result.Error };
        return controller.StatusCode(StatusCodes.Status500InternalServerError, ResponseFactory.Error(controller.HttpContext, Env(controller), ApiResponseCodes.Common.InternalError, errors));
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

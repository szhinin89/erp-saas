using ERP.API.Contracts;
using ERP.Application.Common;
using ERP.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace ERP.API.Extensions;

public static class ApiResultExtensions
{
    public static IActionResult ApiOk<T>(
        this ControllerBase controller,
        T payload,
        string successMessage = "OK")
        => controller.Ok(new ApiResponse<T>(true, successMessage, payload));

    public static IActionResult ApiCreated<T>(
        this ControllerBase controller,
        T payload,
        string successMessage = "Creado")
        => controller.StatusCode(StatusCodes.Status201Created, new ApiResponse<T>(true, successMessage, payload));

    public static IActionResult ApiBadRequest(
        this ControllerBase controller,
        string message = "Error")
        => controller.BadRequest(new ApiResponse<object>(false, message, new { }));

    public static IActionResult ApiUnauthorized(
        this ControllerBase controller,
        string message = "Unauthorized")
        => controller.Unauthorized(new ApiResponse<object>(false, message, new { }));

    public static IActionResult ApiForbidden(
        this ControllerBase controller,
        string message = "Forbidden")
        => controller.StatusCode(StatusCodes.Status403Forbidden, new ApiResponse<object>(false, message, new { }));

    public static IActionResult ApiNotFound(
        this ControllerBase controller,
        string message = "No encontrado")
        => controller.NotFound(new ApiResponse<object>(false, message, new { }));

    public static IActionResult ApiUnprocessableEntity(
        this ControllerBase controller,
        string message = "No se puede completar la operación.")
        => controller.UnprocessableEntity(new ApiResponse<object>(false, message, new { }));

    public static IActionResult ToCreatedOrBadRequest<T>(
        this ControllerBase controller,
        Result<T> result,
        string successMessage = "Creado",
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.StatusCode(StatusCodes.Status201Created, new ApiResponse<T>(true, successMessage, ResolveValue(result, successFallbackFactory)))
            : MapFailure(controller, result.Error, result.ErrorCode);
    }

    public static IActionResult ToOkOrBadRequest<T>(
        this ControllerBase controller,
        Result<T> result,
        string successMessage = "OK",
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.Ok(new ApiResponse<T>(true, successMessage, ResolveValue(result, successFallbackFactory)))
            : MapFailure(controller, result.Error, result.ErrorCode);
    }

    public static IActionResult ApiConflict(
        this ControllerBase controller,
        string message,
        string? errorCode = null)
        => controller.Conflict(new ApiResponse<object>(false, message, new { code = errorCode }));

    private static IActionResult MapFailure(ControllerBase controller, string? error, string? errorCode)
    {
        var message = error ?? "Error";
        if (string.Equals(errorCode, CompanyRucAlreadyExistsException.ErrorCode, StringComparison.Ordinal)
            || string.Equals(errorCode, ResultErrorCodes.UniqueViolation, StringComparison.Ordinal)
            || string.Equals(errorCode, ResultErrorCodes.Conflict, StringComparison.Ordinal))
            return controller.ApiConflict(message, errorCode);

        if (string.Equals(errorCode, ResultErrorCodes.ValidationFailure, StringComparison.Ordinal))
            return controller.ApiUnprocessableEntity(message);

        return controller.BadRequest(new ApiResponse<object>(false, message, new { code = errorCode }));
    }

    public static IActionResult ToOkOrUnauthorized<T>(
        this ControllerBase controller,
        Result<T> result,
        string successMessage = "OK",
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.Ok(new ApiResponse<T>(true, successMessage, ResolveValue(result, successFallbackFactory)))
            : controller.Unauthorized(new ApiResponse<object>(false, result.Error ?? "Unauthorized", new { }));
    }

    public static IActionResult ToOkOrNotFound<T>(
        this ControllerBase controller,
        Result<T> result,
        string successMessage = "OK",
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.Ok(new ApiResponse<T>(true, successMessage, ResolveValue(result, successFallbackFactory)))
            : controller.NotFound(new ApiResponse<object>(false, result.Error ?? "No encontrado", new { }));
    }

    public static IActionResult ToOkOrInternalServerError<T>(
        this ControllerBase controller,
        Result<T> result,
        string successMessage = "OK",
        Func<T>? successFallbackFactory = null)
    {
        return result.IsSuccess
            ? controller.Ok(new ApiResponse<T>(true, successMessage, ResolveValue(result, successFallbackFactory)))
            : controller.StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>(false, result.Error ?? "Error interno del servidor.", new { }));
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

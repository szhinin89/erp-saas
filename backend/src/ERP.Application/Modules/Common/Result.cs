// CA1000: Static factory methods on Result<T> are intentional API design — moving to non-generic Result class
// would break the fluent API used across all use cases.
#pragma warning disable CA1000
using System.Text.Json.Serialization;

namespace ERP.Application.Common;

/// <summary>
/// Encapsula el resultado de un caso de uso, discriminando entre éxito y fallo
/// sin lanzar excepciones para errores de negocio esperados.
///
/// <see cref="Code"/> es un valor de <see cref="ApiResponseCodes"/>: en éxito permite
/// seleccionar un mensaje/severidad distintos del genérico "OK"/"CREATED"; en fallo
/// determina el status HTTP y el mensaje de error vía <c>ApiResultExtensions</c> +
/// <see cref="MessageCatalog"/>.
///
/// Uso en handlers:
///   return Result&lt;Dto&gt;.Failure("El código ya existe.");
///
/// Uso en controllers:
///   return result.ToOkOrBadRequest(...);
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? Code { get; }

    [JsonConstructor]
    private Result(bool isSuccess, T? value, string? error, string? code = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Code = code;
    }

    public static Result<T> Success(T value, string? code = null) => new(true, value, null, code);

    public static Result<T> Failure(string error, string? code = null) => new(false, default, error, code);

    /// <summary>Conflicto de concurrencia o unicidad (HTTP 409).</summary>
    public static Result<T> Conflict(string error, string? code = ApiResponseCodes.Common.Conflict)
        => new(false, default, error, code ?? ApiResponseCodes.Common.Conflict);

    /// <summary>Violación UNIQUE en PostgreSQL (HTTP 409).</summary>
    public static Result<T> UniqueViolation(string error, string? constraintName = null)
        => new(false, default, error, ApiResponseCodes.Common.UniqueViolation);

    /// <summary>Regla de negocio / validación de dominio (HTTP 422).</summary>
    public static Result<T> ValidationFailure(string error, string? code = ApiResponseCodes.Common.ValidationError)
        => new(false, default, error, code ?? ApiResponseCodes.Common.ValidationError);

    /// <summary>Entidad no encontrada (HTTP 404).</summary>
    public static Result<T> NotFound(string error)
        => new(false, default, error, ApiResponseCodes.Common.NotFound);

    /// <summary>Acceso denegado (HTTP 403).</summary>
    public static Result<T> Forbidden(string error)
        => new(false, default, error, ApiResponseCodes.Common.Forbidden);
}

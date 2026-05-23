using System.Text.Json.Serialization;

namespace ERP.Application.Common;

/// <summary>
/// Encapsula el resultado de un caso de uso, discriminando entre éxito y fallo
/// sin lanzar excepciones para errores de negocio esperados.
///
/// Uso en handlers:
///   return Result&lt;Dto&gt;.Failure("El código ya existe.");
///
/// Uso en controllers:
///   return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error, string? errorCode = null)
    {
        IsSuccess = false;
        Error = error;
        ErrorCode = errorCode;
    }

    /// <summary>Constructor para deserialización JSON (p. ej. caché distribuida).</summary>
    [JsonConstructor]
    private Result(bool isSuccess, T? value, string? error, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error, string? errorCode = null) => new(error, errorCode);

    /// <summary>Conflicto de concurrencia o unicidad (HTTP 409).</summary>
    public static Result<T> Conflict(string error, string? errorCode = ResultErrorCodes.Conflict)
        => new(error, errorCode ?? ResultErrorCodes.Conflict);

    /// <summary>Violación UNIQUE en PostgreSQL (HTTP 409).</summary>
    public static Result<T> UniqueViolation(string error, string? constraintName = null)
        => new(error, ResultErrorCodes.UniqueViolation);

    /// <summary>Regla de negocio / validación de dominio (HTTP 422).</summary>
    public static Result<T> ValidationFailure(string error, string? errorCode = ResultErrorCodes.ValidationFailure)
        => new(error, errorCode ?? ResultErrorCodes.ValidationFailure);

    /// <summary>Entidad no encontrada (HTTP 404).</summary>
    public static Result<T> NotFound(string error)
        => new(error, ResultErrorCodes.NotFound);

    /// <summary>Acceso denegado (HTTP 403).</summary>
    public static Result<T> Forbidden(string error)
        => new(error, ResultErrorCodes.Forbidden);
}

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

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(string error) { IsSuccess = false; Error = error; }

    /// <summary>Constructor para deserialización JSON (p. ej. caché distribuida).</summary>
    [JsonConstructor]
    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}

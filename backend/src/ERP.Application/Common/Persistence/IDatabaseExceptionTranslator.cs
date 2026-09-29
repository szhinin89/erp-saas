namespace ERP.Application.Common.Persistence;

/// <summary>Traduce excepciones de persistencia a errores de dominio (sin depender de Npgsql en Application).</summary>
public interface IDatabaseExceptionTranslator
{
    bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info);

    /// <summary>
    /// Único punto de clasificación técnica de fallos de base de datos (ZH-BACKEND-SECURITY-ERROR-
    /// FINAL-HARDENING-01). Recorre la cadena de InnerException y devuelve el código canónico de
    /// <see cref="ApiResponseCodes"/>, o <c>null</c> si la excepción no proviene de la base de datos:
    /// <list type="bullet">
    /// <item>conexión / timeout / servidor no disponible → DATABASE_UNAVAILABLE (503);</item>
    /// <item>UNIQUE → UNIQUE_VIOLATION (409); otra violación de integridad (FK, CHECK, NOT NULL,
    /// exclusión) → CONFLICT (409); concurrencia optimista, serialización o deadlock →
    /// CONCURRENCY_CONFLICT (409);</item>
    /// <item>cualquier otro error de base de datos (SQL inesperado) → INTERNAL_ERROR (500).</item>
    /// </list>
    /// Nunca devuelve el texto del error: el detalle técnico queda para el log.
    /// </summary>
    string? ClassifyFailureCode(Exception exception);
}

/// <summary>Metadatos de violación UNIQUE PostgreSQL (SqlState 23505).</summary>
public sealed record DatabaseUniqueViolationInfo(
    string SqlState,
    string? ConstraintName,
    string? TableName,
    string? Detail
);

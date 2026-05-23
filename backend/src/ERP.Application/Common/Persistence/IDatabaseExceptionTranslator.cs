namespace ERP.Application.Common.Persistence;

/// <summary>Traduce excepciones de persistencia a errores de dominio (sin depender de Npgsql en Application).</summary>
public interface IDatabaseExceptionTranslator
{
    bool TryGetUniqueViolation(Exception exception, out DatabaseUniqueViolationInfo info);
}

/// <summary>Metadatos de violación UNIQUE PostgreSQL (SqlState 23505).</summary>
public sealed record DatabaseUniqueViolationInfo(
    string SqlState,
    string? ConstraintName,
    string? TableName,
    string? Detail);

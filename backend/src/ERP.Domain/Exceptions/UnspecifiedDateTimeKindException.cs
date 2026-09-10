namespace ERP.Domain.Exceptions;

/// <summary>
/// ZH-DATETIME-UTC-GUARDRAILS-01: guardrail global (ErpDbContext.SaveChanges) que impide
/// persistir un <see cref="DateTime"/> con Kind Unspecified o Local. Ningún DateTime debe
/// llegar a PostgreSQL (timestamptz) sin Kind=Utc — usar
/// <see cref="ERP.Domain.Common.UtcDateTime"/> para normalizar en el punto de origen
/// (entidad/factory/mutador), no como parche en el handler.
///
/// Es una violación de invariante de infraestructura, no un error de base de datos ni una
/// regla de negocio del dominio: ExceptionMiddleware la mapea a un código dedicado, nunca a
/// DATABASE_UNAVAILABLE ni DOMAIN_RULE_VIOLATION.
/// </summary>
public sealed class UnspecifiedDateTimeKindException : Exception
{
    public string Code { get; } = "unspecified_datetime_kind";

    public UnspecifiedDateTimeKindException(string entityName, string propertyName, DateTimeKind kind)
        : base(
            $"{entityName}.{propertyName} tiene DateTimeKind.{kind}. Todo DateTime persistible debe "
                + "normalizarse a UTC (DateTimeKind.Utc) antes de llegar a SaveChanges — usar "
                + "ERP.Domain.Common.UtcDateTime.Normalize() en la entidad/factory/mutador de origen."
        ) { }
}

namespace ERP.Domain.Exceptions;

/// <summary>
/// Se lanza cuando se intenta una mutación no permitida sobre un registro sembrado automáticamente
/// por el Bootstrap del sistema. Regla de negocio: subclase de
/// <see cref="DomainRuleViolationException"/> (→ DOMAIN_RULE_VIOLATION, 422). Ver
/// <c>ERP.Domain.Common.ISystemSeeded</c> / <c>SystemSeedGuard</c>.
/// </summary>
public sealed class SystemSeededRecordException : DomainRuleViolationException
{
    public SystemSeededRecordException(string entityLabel, string action)
        : base($"{entityLabel} es un registro sembrado por el sistema y no puede {action}.") { }
}

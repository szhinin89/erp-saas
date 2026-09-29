namespace ERP.Domain.Exceptions;

/// <summary>
/// ÚNICO mecanismo para una regla de negocio conocida que el dominio rechaza (ZH-DOMAIN-RULE-ERROR-SSOT-01):
/// estado que no admite la operación, dato de negocio inválido, transición no permitida. Su
/// <see cref="Exception.Message"/> es el mensaje público curado — forma parte del contrato funcional.
/// Viaja siempre como <c>DOMAIN_RULE_VIOLATION</c> (422): por MediatR vía <c>DomainRuleBehavior</c>
/// (→ <c>Result.FromDomainRule</c>) y fuera de MediatR vía <c>ExceptionMiddleware</c>.
/// <para>
/// No deriva de <see cref="InvalidOperationException"/> a propósito: una
/// <see cref="InvalidOperationException"/> significa solo error interno / estado imposible /
/// programación y termina en 500 sin exponer su texto.
/// </para>
/// </summary>
public class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(string message)
        : base(message) { }

    public DomainRuleViolationException(string message, Exception innerException)
        : base(message, innerException) { }
}

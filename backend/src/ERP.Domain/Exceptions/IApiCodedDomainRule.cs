namespace ERP.Domain.Exceptions;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01 — una <see cref="DomainRuleViolationException"/> que además declara
/// un código público estable (SCREAMING_SNAKE_CASE, registrado en <c>ApiResponseCodes</c> y en
/// <c>MessageCatalog</c>) para que el cliente distinga la regla sin interpretar el mensaje. Sigue
/// siendo una regla de dominio (422): <c>Result.FromDomainRule</c> y <c>ExceptionMiddleware</c> usan
/// este código en lugar de <c>DOMAIN_RULE_VIOLATION</c>. Opt-in: las demás reglas no cambian.
/// </summary>
public interface IApiCodedDomainRule
{
    string ApiCode { get; }
}

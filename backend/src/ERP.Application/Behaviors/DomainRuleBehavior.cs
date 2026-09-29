using ERP.Application.Common;
using ERP.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ERP.Application.Behaviors;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01 — traducción única regla de negocio → respuesta para toda request
/// MediatR cuya respuesta es un <see cref="Result{T}"/>: una <see cref="DomainRuleViolationException"/>
/// que escapa del handler (o de un evento de dominio publicado en su SaveChanges) se convierte en
/// <c>Result.FromDomainRule</c> (DOMAIN_RULE_VIOLATION, 422, mensaje público). Los handlers no
/// capturan reglas de dominio para devolver <c>ValidationFailure(ex.Message)</c>; si necesitan
/// deshacer una transacción, hacen rollback y relanzan.
/// <para>
/// Cualquier otra excepción (incluida <see cref="InvalidOperationException"/>: error interno,
/// estado imposible, framework) atraviesa el behavior sin cambios y termina en
/// <c>ExceptionMiddleware</c> como 500 sanitizado.
/// </para>
/// El constraint sobre <typeparamref name="TResponse"/> hace que el contenedor omita este behavior
/// para respuestas que no son <see cref="Result{T}"/>; esas requests dejan subir la excepción, que
/// <c>ExceptionMiddleware</c> traduce con el mismo código y mensaje.
/// </summary>
public sealed partial class DomainRuleBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : IDomainRuleResult<TResponse>
{
    private readonly ILogger<DomainRuleBehavior<TRequest, TResponse>> _logger;

    public DomainRuleBehavior(ILogger<DomainRuleBehavior<TRequest, TResponse>> logger) =>
        _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (DomainRuleViolationException violation)
        {
            LogRuleRejected(typeof(TRequest).Name, violation.Message);
            return TResponse.FromDomainRule(violation);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "{Request} rechazada por regla de negocio: {Reason}"
    )]
    private partial void LogRuleRejected(string request, string reason);
}

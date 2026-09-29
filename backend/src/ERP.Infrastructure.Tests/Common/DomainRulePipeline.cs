using ERP.Application.Behaviors;
using ERP.Application.Common;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Infrastructure.Tests.Common;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01 — ejecuta un handler envuelto en el <see cref="DomainRuleBehavior{TRequest, TResponse}"/>
/// real, igual que el pipeline de MediatR en producción: una <c>DomainRuleViolationException</c> se
/// convierte en <c>Result.FromDomainRule</c>. Para tests de integración que invocan el handler
/// directamente y verifican el Result de un rechazo por regla de negocio.
/// </summary>
internal static class DomainRulePipeline
{
    public static Task<TResponse> HandleWithDomainRules<TRequest, TResponse>(
        this IRequestHandler<TRequest, TResponse> handler,
        TRequest request,
        CancellationToken cancellationToken
    )
        where TRequest : IRequest<TResponse>
        where TResponse : IDomainRuleResult<TResponse> =>
        new DomainRuleBehavior<TRequest, TResponse>(
            NullLogger<DomainRuleBehavior<TRequest, TResponse>>.Instance
        ).Handle(request, ct => handler.Handle(request, ct), cancellationToken);
}

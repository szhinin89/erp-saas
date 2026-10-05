using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Purchases.Exceptions;
using ERP.Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERP.Application.Tests.Behaviors;

/// <summary>
/// ZH-DOMAIN-RULE-ERROR-SSOT-01 — <see cref="DomainRuleBehavior{TRequest, TResponse}"/> es la traducción
/// única regla de negocio → Result para toda request MediatR con respuesta Result&lt;T&gt;.
/// </summary>
public sealed class DomainRuleBehaviorTests
{
    private sealed record ResultRequest : IRequest<Result<string>>;

    private sealed record UnitRequest : IRequest<Unit>;

    private static Task<Result<string>> Run(Func<Task<Result<string>>> handler) =>
        new DomainRuleBehavior<ResultRequest, Result<string>>(
            NullLogger<DomainRuleBehavior<ResultRequest, Result<string>>>.Instance
        ).Handle(new ResultRequest(), _ => handler(), CancellationToken.None);

    [Fact]
    public async Task Regla_de_negocio_se_traduce_a_DOMAIN_RULE_VIOLATION_con_el_mensaje_publico()
    {
        var result = await Run(() =>
            throw new DomainRuleViolationException("La factura ya está anulada.")
        );

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        result.Error.Should().Be("La factura ya está anulada.");
    }

    [Fact]
    public async Task Subclases_semanticas_usan_el_mismo_mecanismo()
    {
        var seeded = await Run(() =>
            throw new SystemSeededRecordException("La bodega principal", "deshabilitarse")
        );
        var posting = await Run(() =>
            throw new PurchasePostingFailedException("Período cerrado.", "PERIOD_NOT_OPEN")
        );

        seeded.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        posting.Code.Should().Be(ApiResponseCodes.Common.DomainRuleViolation);
        posting.Error.Should().Be("Período cerrado.");
    }

    [Fact]
    public async Task InvalidOperationException_es_tecnica_y_atraviesa_el_behavior_sin_traducirse()
    {
        var act = () =>
            Run(() => throw new InvalidOperationException("Sequence contains no elements"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Exito_no_cambia()
    {
        var result = await Run(() => Task.FromResult(Result<string>.Success("ok")));

        result.Value.Should().Be("ok");
    }

    [Fact]
    public void FromDomainRule_es_la_unica_traduccion_y_se_construye_desde_la_excepcion()
    {
        var result = Result<int>.FromDomainRule(new DomainRuleViolationException("Regla."));

        (result.IsSuccess, result.Code, result.Error)
            .Should()
            .Be((false, ApiResponseCodes.Common.DomainRuleViolation, "Regla."));
    }

    [Fact]
    public void DomainRuleViolationException_no_es_InvalidOperationException()
    {
        typeof(InvalidOperationException)
            .IsAssignableFrom(typeof(DomainRuleViolationException))
            .Should()
            .BeFalse();
        typeof(DomainRuleViolationException)
            .IsAssignableFrom(typeof(SystemSeededRecordException))
            .Should()
            .BeTrue();
        typeof(DomainRuleViolationException)
            .IsAssignableFrom(typeof(DocumentFlowPolicyViolationException))
            .Should()
            .BeTrue();
    }

    // ── Registro en el contenedor ──

    [Fact]
    public void Contenedor_aplica_el_behavior_a_Result_y_lo_omite_para_otras_respuestas()
    {
        // Mismo registro open-generic que AddApplication, en un contenedor real de Microsoft.Extensions.DI.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DomainRuleBehavior<,>));
        using var provider = services.BuildServiceProvider();

        provider
            .GetServices<IPipelineBehavior<ResultRequest, Result<string>>>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<DomainRuleBehavior<ResultRequest, Result<string>>>();
        provider
            .GetServices<IPipelineBehavior<UnitRequest, Unit>>()
            .Should()
            .BeEmpty("el constraint IDomainRuleResult excluye respuestas que no son Result<T>");
    }

    [Fact]
    public void AddApplication_lo_registra_antes_de_Caching_para_que_un_rechazo_nunca_se_guarde_en_cache()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        var order = services
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        order.Should().Contain(typeof(DomainRuleBehavior<,>));
        order
            .IndexOf(typeof(DomainRuleBehavior<,>))
            .Should()
            .BeLessThan(order.IndexOf(typeof(CachingBehavior<,>)));
    }
}

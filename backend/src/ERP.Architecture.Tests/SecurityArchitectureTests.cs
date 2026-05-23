using ERP.Application.Common;
using FluentAssertions;
using MediatR;
using System.Reflection;

namespace ERP.Architecture.Tests;

/// <summary>
/// Tests de arquitectura de seguridad — se ejecutan en CI como guardrails.
///
/// PROPÓSITO: Garantizar que el modelo de seguridad multiempresa se mantenga consistente
/// a medida que crece el codebase. Falla en compilación/test si un developer viola las reglas.
///
/// REGLAS:
///   AR-SEC-1: Handlers en namespaces MasterData que implementan ISubscriberOnlyRequest
///             NO deben implementar también ICompanyScopedRequest (conflicto de contratos).
///
///   AR-SEC-2: Toda entidad que implementa ICompanyScopedEntity también debe implementar
///             ISubscriberScopedEntity (query filter requiere ambas para el filtro estricto).
///
///   AR-SEC-3: Los commands/queries en MasterData namespace deben declarar explícitamente
///             su scope: ISubscriberOnlyRequest O ICompanyScopedRequest — no ambiguos.
///
///   AR-SEC-4: No debe existir ningún IRequestHandler en namespaces operativos ERP
///             cuyo command/query no implemente ninguno de los marcadores de scope.
///             (Documenta handlers que dependen solo del namespace-prefix — deuda conocida.)
/// </summary>
public sealed class SecurityArchitectureTests
{
    private static readonly Assembly AppAssembly =
        typeof(ERP.Application.Common.ICurrentSubscriber).Assembly;

    private static readonly Assembly DomainAssembly =
        typeof(ERP.Domain.Common.ISubscriberScopedEntity).Assembly;

    // ── AR-SEC-1: ISubscriberOnlyRequest XOR ICompanyScopedRequest ───────────────

    [Fact]
    public void AR_SEC_1_subscriber_only_request_must_not_also_implement_company_scoped()
    {
        var types = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ISubscriberOnlyRequest).IsAssignableFrom(t)
                     && typeof(ICompanyScopedRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        types.Should().BeEmpty(
            "un request no puede ser ISubscriberOnlyRequest Y ICompanyScopedRequest al mismo tiempo — " +
            "son contratos mutuamente excluyentes");
    }

    // ── AR-SEC-2: ICompanyScopedEntity implica ISubscriberScopedEntity ───────────

    [Fact]
    public void AR_SEC_2_company_scoped_entity_must_also_implement_subscriber_scoped()
    {
        var violators = DomainAssembly.GetTypes()
            .Concat(AppAssembly.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ERP.Domain.Common.ICompanyScopedEntity).IsAssignableFrom(t))
            .Where(t => !typeof(ERP.Domain.Common.ISubscriberScopedEntity).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "toda entidad con ICompanyScopedEntity debe también implementar ISubscriberScopedEntity " +
            "para que el query filter dual (subscriber + company) funcione correctamente");
    }

    // ── AR-SEC-3: Commands en MasterData deben declarar scope explícito ──────────

    [Fact]
    public void AR_SEC_3_masterdata_commands_must_declare_explicit_scope()
    {
        const string masterDataPrefix = "ERP.Application.MasterData";

        var types = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => (t.Namespace ?? string.Empty).StartsWith(masterDataPrefix))
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !typeof(ISubscriberOnlyRequest).IsAssignableFrom(t)
                     && !typeof(ICompanyScopedRequest).IsAssignableFrom(t)
                     && !typeof(IRequiresCompanyContext).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        types.Should().BeEmpty(
            "todos los commands/queries en namespace MasterData deben declarar explícitamente " +
            "ISubscriberOnlyRequest, ICompanyScopedRequest o IRequiresCompanyContext. " +
            "No se acepta dependencia implícita del namespace-prefix de CompanyScopeBehavior");
    }

    // ── AR-SEC-4: Inventario de handlers que dependen de namespace-prefix (deuda) ─

    [Fact]
    public void AR_SEC_4_document_handlers_relying_on_namespace_prefix_security()
    {
        // Estos namespaces están cubiertos por CompanyScopeBehavior.CompanyScopedNamespacePrefixes.
        // Los handlers aquí SON seguros (namespace-prefix los protege), pero son deuda técnica.
        // Este test documenta cuántos existen — no falla, sino que reporta el número.
        // Objetivo: reducir a 0 eventualmente mediante migración a IRequiresCompanyContext explícito.

        var namespacesCubiertos = new[]
        {
            "ERP.Application.Sales",
            "ERP.Application.Modules.Inventory",
            "ERP.Application.Inventory",
            "ERP.Application.Products",
            "ERP.Application.Modules.Products",
            "ERP.Application.Modules.Purchasing",
            "ERP.Application.Purchasing",
            "ERP.Application.Modules.Accounting",
            "ERP.Application.Accounting",
            "ERP.Application.Modules.Cash",
            "ERP.Application.Cash",
        };

        var handlersConDeuda = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
            {
                var ns = t.Namespace ?? string.Empty;
                return namespacesCubiertos.Any(p => ns.StartsWith(p, StringComparison.Ordinal));
            })
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !typeof(ISubscriberOnlyRequest).IsAssignableFrom(t)
                     && !typeof(ICompanyScopedRequest).IsAssignableFrom(t)
                     && !typeof(IRequiresCompanyContext).IsAssignableFrom(t))
            .ToList();

        // NO falla — documenta deuda técnica como información.
        // La regla es: este número no debe CRECER. Migrar handlers a IRequiresCompanyContext gradualmente.
        (handlersConDeuda.Count <= 200).Should().BeTrue(
            $"hay {handlersConDeuda.Count} handlers que dependen del namespace-prefix para company scope. " +
            "Migrar a IRequiresCompanyContext o ICompanyScopedRequest gradualmente.");
    }
}

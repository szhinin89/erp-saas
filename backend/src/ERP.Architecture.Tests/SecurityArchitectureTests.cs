using ERP.Application.Common;
using FluentAssertions;
using MediatR;
using System.Reflection;

namespace ERP.Architecture.Tests;

/// <summary>
/// Guardrails de seguridad multiempresa — CI bloqueante.
/// </summary>
public sealed class SecurityArchitectureTests
{
    private static readonly Assembly AppAssembly =
        typeof(ICurrentSubscriber).Assembly;

    private static readonly Assembly DomainAssembly =
        typeof(ERP.Domain.Common.ISubscriberScopedEntity).Assembly;

    private static readonly string[] OperationalErpNamespacePrefixes =
    [
        "ERP.Application.Sales",
        "ERP.Application.Modules.Sales",
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
        "ERP.Application.Modules.Logistics",
        "ERP.Application.Logistics",
        "ERP.Application.Modules.Branches",
        "ERP.Application.Modules.Expenses",
        "ERP.Application.Modules.Configuration",
    ];

    private static readonly string[] NamespacePrefixDebtPrefixes =
    [
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
    ];

    [Fact]
    public void AR_SEC_1_subscriber_scoped_request_must_not_also_implement_company_scoped()
    {
        var types = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ISubscriberScopedRequest).IsAssignableFrom(t)
                     && typeof(ICompanyScopedRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        types.Should().BeEmpty(
            "un request no puede ser ISubscriberScopedRequest e ICompanyScopedRequest a la vez");
    }

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
            "toda entidad con ICompanyScopedEntity debe también implementar ISubscriberScopedEntity");
    }

    [Fact]
    public void AR_SEC_3_masterdata_commands_must_declare_explicit_scope()
    {
        const string masterDataPrefix = "ERP.Application.MasterData";

        var types = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => (t.Namespace ?? string.Empty).StartsWith(masterDataPrefix))
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !HasExplicitScopeMarker(t))
            .Select(t => t.FullName)
            .ToList();

        types.Should().BeEmpty(
            "MasterData debe declarar ISubscriberScopedRequest, ICompanyScopedRequest o IPlatformScopedRequest");
    }

    [Fact]
    public void AR_SEC_4_operational_erp_requests_must_declare_explicit_scope_not_namespace_fallback()
    {
        var violators = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => OperationalErpNamespacePrefixes.Any(p =>
                (t.Namespace ?? string.Empty).StartsWith(p, StringComparison.Ordinal)))
            .Where(t => !HasExplicitScopeMarker(t))
            .Select(t => t.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "handlers ERP operativos deben implementar ICompanyScopedRequest (o IPlatformScopedRequest para jobs de sistema). " +
            "El namespace-prefix es solo fallback legacy temporal");
    }

    [Fact]
    public void AR_SEC_5_namespace_prefix_debt_must_not_grow()
    {
        var handlersConDeuda = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
            {
                var ns = t.Namespace ?? string.Empty;
                return NamespacePrefixDebtPrefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal));
            })
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !HasExplicitScopeMarker(t))
            .ToList();

        handlersConDeuda.Should().BeEmpty(
            $"quedan {handlersConDeuda.Count} requests sin marcador explícito en namespaces con deuda de prefix");
    }

    private static bool HasExplicitScopeMarker(Type t) =>
        typeof(ICompanyScopedRequest).IsAssignableFrom(t)
        || typeof(ISubscriberScopedRequest).IsAssignableFrom(t)
        || typeof(IPlatformScopedRequest).IsAssignableFrom(t);
}

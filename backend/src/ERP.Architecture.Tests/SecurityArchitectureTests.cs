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
    private static readonly Assembly AppAssembly = typeof(ICurrentTenant).Assembly;

    private static readonly Assembly DomainAssembly =
        typeof(ERP.Domain.Common.ITenantScopedEntity).Assembly;

    private static readonly string[] OperationalErpNamespacePrefixes =
    [
        "ERP.Application.Sales",
        "ERP.Application.Modules.Sales",
        "ERP.Application.Modules.Inventory",
        "ERP.Application.Inventory",
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
        var types = AppAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
                typeof(ITenantScopedRequest).IsAssignableFrom(t)
                && typeof(ICompanyScopedRequest).IsAssignableFrom(t)
            )
            .Select(t => t.FullName)
            .ToList();

        types
            .Should()
            .BeEmpty(
                "un request no puede ser ITenantScopedRequest e ICompanyScopedRequest a la vez"
            );
    }

    [Fact]
    public void AR_SEC_2_company_scoped_entity_must_also_implement_subscriber_scoped()
    {
        var violators = DomainAssembly
            .GetTypes()
            .Concat(AppAssembly.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ERP.Domain.Common.ICompanyScopedEntity).IsAssignableFrom(t))
            .Where(t => !typeof(ERP.Domain.Common.ITenantScopedEntity).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        violators
            .Should()
            .BeEmpty(
                "toda entidad con ICompanyScopedEntity debe también implementar ITenantScopedEntity"
            );
    }

    [Fact]
    public void AR_SEC_3_masterdata_commands_must_declare_explicit_scope()
    {
        const string masterDataPrefix = "ERP.Application.MasterData";

        var types = AppAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
                (t.Namespace ?? string.Empty).StartsWith(masterDataPrefix, StringComparison.Ordinal)
            )
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !HasExplicitScopeMarker(t))
            .Select(t => t.FullName)
            .ToList();

        types
            .Should()
            .BeEmpty(
                "MasterData debe declarar ITenantScopedRequest, ICompanyScopedRequest o IPlatformScopedRequest"
            );
    }

    [Fact]
    public void AR_SEC_4_operational_erp_requests_must_declare_explicit_scope_not_namespace_fallback()
    {
        var violators = AppAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t =>
                OperationalErpNamespacePrefixes.Any(p =>
                    (t.Namespace ?? string.Empty).StartsWith(p, StringComparison.Ordinal)
                )
            )
            .Where(t => !HasExplicitScopeMarker(t))
            .Select(t => t.FullName)
            .ToList();

        violators
            .Should()
            .BeEmpty(
                "handlers ERP operativos deben implementar ICompanyScopedRequest (o IPlatformScopedRequest para jobs de sistema). "
                    + "El namespace-prefix es solo fallback legacy temporal"
            );
    }

    [Fact]
    public void AR_SEC_5_namespace_prefix_debt_must_not_grow()
    {
        var handlersConDeuda = AppAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t =>
            {
                var ns = t.Namespace ?? string.Empty;
                return NamespacePrefixDebtPrefixes.Any(p =>
                    ns.StartsWith(p, StringComparison.Ordinal)
                );
            })
            .Where(t => typeof(IBaseRequest).IsAssignableFrom(t))
            .Where(t => !HasExplicitScopeMarker(t))
            .ToList();

        handlersConDeuda
            .Should()
            .BeEmpty(
                $"quedan {handlersConDeuda.Count} requests sin marcador explícito en namespaces con deuda de prefix"
            );
    }

    private static readonly Assembly InfraAssembly =
        typeof(ERP.Infrastructure.Persistence.ErpDbContext).Assembly;

    /// <summary>
    /// AR-SEC-6: Ninguna entidad con ICompanyOperationalEntity puede tener CompanyId como Guid?
    /// CompanyId debe ser Guid (NOT NULL) tras la migración final.
    /// </summary>
    [Fact]
    public void AR_SEC_6_company_operational_entities_must_have_non_nullable_company_id()
    {
        var violators = DomainAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ERP.Domain.Common.ICompanyOperationalEntity).IsAssignableFrom(t))
            .Where(t =>
            {
                var prop = t.GetProperty("CompanyId");
                if (prop is null)
                    return false;
                return prop.PropertyType == typeof(Guid?);
            })
            .Select(t => t.FullName)
            .ToList();

        violators
            .Should()
            .BeEmpty(
                "ICompanyOperationalEntity.CompanyId debe ser Guid (NOT NULL). "
                    + "Migración FinalizeCompanyIdNotNull ya fue aplicada. "
                    + "Ninguna nueva entidad operacional puede tener CompanyId nullable."
            );
    }

    /// <summary>
    /// AR-SEC-7: Los tres interceptors de tenant deben estar registrados en Infrastructure.
    /// Garantiza que no se eliminaron accidentalmente las capas de protección.
    /// </summary>
    [Fact]
    public void AR_SEC_7_all_tenant_interceptors_must_exist()
    {
        var interceptorTypes = new[]
        {
            "ERP.Infrastructure.Persistence.Interceptors.CompanyTenantInterceptor",
            "ERP.Infrastructure.Persistence.Interceptors.DbCommandTenantInterceptor",
            "ERP.Infrastructure.Persistence.Interceptors.PostgreSqlSessionContextInterceptor",
        };

        foreach (var typeName in interceptorTypes)
        {
            var type = InfraAssembly.GetType(typeName);
            type.Should()
                .NotBeNull(
                    $"El interceptor {typeName} debe existir. "
                        + "Es parte del Zero-Leak enforcement layer y no puede eliminarse."
                );
        }
    }

    /// <summary>
    /// AR-SEC-8: Domain COMPANY no puede importar entidades de SUBSCRIBER billing.
    /// Cross-domain reference prohibition.
    /// </summary>
    [Fact]
    public void AR_SEC_8_company_domain_must_not_reference_subscriber_billing()
    {
        var forbiddenNamespaces = new[]
        {
            "ERP.Domain.Billing",
            "ERP.Domain.Tenants.Entities.SubscriberBillingAccount",
            "ERP.Domain.Subscriptions",
        };

        var erpOperationalNamespaces = new[]
        {
            "ERP.Domain.Modules.Sales",
            "ERP.Domain.Modules.Purchasing",
            "ERP.Domain.Modules.Inventory",
            "ERP.Domain.Modules.Accounting",
            "ERP.Domain.Modules.Cash",
        };

        var violators = DomainAssembly
            .GetTypes()
            .Where(t =>
                erpOperationalNamespaces.Any(ns =>
                    (t.Namespace ?? "").StartsWith(ns, StringComparison.Ordinal)
                )
            )
            .Where(t =>
                t.GetFields(
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    )
                    .Any(f =>
                        forbiddenNamespaces.Any(fn =>
                            (f.FieldType.Namespace ?? "").StartsWith(fn, StringComparison.Ordinal)
                        )
                    )
            )
            .Select(t => t.FullName)
            .ToList();

        violators
            .Should()
            .BeEmpty(
                "Entidades COMPANY no deben referenciar entidades SUBSCRIBER billing. "
                    + "Violación del boundary COMPANY ↔ SUBSCRIBER."
            );
    }

    /// <summary>
    /// AR-SEC-9: ICompanyScopedEntity implementors deben tener CompanyId como Guid (NOT NULL).
    /// </summary>
    [Fact]
    public void AR_SEC_9_company_scoped_entities_must_have_non_nullable_company_id()
    {
        var violators = DomainAssembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ERP.Domain.Common.ICompanyScopedEntity).IsAssignableFrom(t))
            .Where(t =>
            {
                var prop = t.GetProperty("CompanyId");
                if (prop is null)
                    return false;
                return prop.PropertyType == typeof(Guid?);
            })
            .Select(t => t.FullName)
            .ToList();

        violators
            .Should()
            .BeEmpty(
                "ICompanyScopedEntity.CompanyId debe ser Guid (NOT NULL). "
                    + "Entidades con company scope estricto nunca pueden tener CompanyId nullable."
            );
    }

    private static bool HasExplicitScopeMarker(Type t) =>
        typeof(ICompanyScopedRequest).IsAssignableFrom(t)
        || typeof(ITenantScopedRequest).IsAssignableFrom(t)
        || typeof(IPlatformScopedRequest).IsAssignableFrom(t);
}

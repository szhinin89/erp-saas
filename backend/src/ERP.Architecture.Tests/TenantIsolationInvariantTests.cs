using ERP.Domain.Common;
using FluentAssertions;
using System.Reflection;

namespace ERP.Architecture.Tests;

/// <summary>
/// Invariants de aislamiento multi-tenant — CI bloqueante.
/// Verifica garantías estructurales antes de testing funcional.
/// </summary>
public sealed class TenantIsolationInvariantTests
{
    private static readonly Assembly DomainAssembly =
        typeof(ITenantScopedEntity).Assembly;

    private static readonly Assembly InfraAssembly =
        typeof(ERP.Infrastructure.Persistence.ErpDbContext).Assembly;

    private static readonly Assembly AppAssembly =
        typeof(ERP.Application.Common.ICurrentTenant).Assembly;

    // ── INVARIANT A — CompanyId inmutable ────────────────────────────────────

    /// <summary>
    /// INV-A1: Ninguna entidad operacional puede tener CompanyId con setter público.
    /// CompanyId es inmutable post-creación — solo se puede asignar en el factory.
    /// </summary>
    [Fact]
    public void INV_A1_company_id_must_not_have_public_setter()
    {
        var operationalInterfaces = new[]
        {
            typeof(ICompanyOperationalEntity),
            typeof(ICompanyScopedEntity),
        };

        var violators = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => operationalInterfaces.Any(i => i.IsAssignableFrom(t)))
            .Select(t => new
            {
                Type = t,
                Prop = t.GetProperty("CompanyId", BindingFlags.Public | BindingFlags.Instance)
            })
            .Where(x => x.Prop is not null)
            .Where(x =>
            {
                var setter = x.Prop!.SetMethod;
                return setter is not null && setter.IsPublic;
            })
            .Select(x => x.Type.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "CompanyId es un invariante de identidad. " +
            "Setters públicos permiten re-asignación cross-company. " +
            "Usar factory Create() para asignar CompanyId.");
    }

    /// <summary>
    /// INV-A2: Toda entidad operacional con ICompanyOperationalEntity debe tener CompanyId como Guid (NOT NULL).
    /// Garantiza que la migración FinalizeCompanyIdNotNull fue correcta.
    /// </summary>
    [Fact]
    public void INV_A2_operational_entity_company_id_must_be_guid_not_nullable()
    {
        var violators = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ICompanyOperationalEntity).IsAssignableFrom(t))
            .Where(t =>
            {
                var prop = t.GetProperty("CompanyId");
                return prop?.PropertyType == typeof(Guid?);
            })
            .Select(t => t.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "ICompanyOperationalEntity.CompanyId debe ser Guid (NOT NULL). " +
            "Todas las entidades operacionales requieren empresa válida. " +
            "Migración FinalizeCompanyIdNotNull establece esta garantía.");
    }

    // ── INVARIANT B — TenantId consistente ────────────────────────────────────

    /// <summary>
    /// INV-B1: Ninguna entidad operacional (COMPANY scope) puede tener TenantId con setter público.
    /// TenantId es parte de la identidad del tenant — no es editable.
    /// </summary>
    [Fact]
    public void INV_B1_tenant_id_must_not_have_public_setter_on_operational_entities()
    {
        var violators = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ICompanyOperationalEntity).IsAssignableFrom(t)
                     || typeof(ICompanyScopedEntity).IsAssignableFrom(t))
            .Select(t => new
            {
                Type = t,
                Prop = t.GetProperty("TenantId", BindingFlags.Public | BindingFlags.Instance)
            })
            .Where(x => x.Prop is not null)
            .Where(x =>
            {
                var setter = x.Prop!.SetMethod;
                return setter is not null && setter.IsPublic;
            })
            .Select(x => x.Type.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "TenantId es un invariante de tenancy — no puede tener setter público. " +
            "Cambiar TenantId permitiría mover datos entre tenants.");
    }

    // ── INVARIANT C — Ownership Company ↔ Subscriber ─────────────────────────

    /// <summary>
    /// INV-C1: ICompanyScopedEntity también debe implementar ITenantScopedEntity.
    /// Una company siempre pertenece a un tenant — el scope de empresa implica el de tenant.
    /// </summary>
    [Fact]
    public void INV_C1_company_scoped_entity_must_also_be_tenant_scoped()
    {
        var violators = DomainAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(ICompanyScopedEntity).IsAssignableFrom(t))
            .Where(t => !typeof(ITenantScopedEntity).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "Toda entidad company-scoped debe también ser tenant-scoped. " +
            "Una empresa siempre pertenece a un tenant.");
    }

    /// <summary>
    /// INV-C2: ICompanyOperationalEntity (que extiende ITenantScopedEntity)
    /// no puede existir sin TenantId.
    /// </summary>
    [Fact]
    public void INV_C2_company_operational_entity_inherits_tenant_scope()
    {
        // ICompanyOperationalEntity extends ITenantScopedEntity — verified at compile time
        // This test verifies the runtime type hierarchy is correct
        typeof(ICompanyOperationalEntity).GetInterfaces()
            .Should().Contain(typeof(ITenantScopedEntity),
            "ICompanyOperationalEntity debe extender ITenantScopedEntity. " +
            "Toda entidad operacional requiere contexto de tenant.");
    }

    // ── INVARIANT D — No Cross-Company References ─────────────────────────────

    /// <summary>
    /// INV-D1: Módulos ERP operativos no deben importar entidades SUBSCRIBER billing.
    /// Garantiza separación estricta entre control plane y data plane.
    /// </summary>
    [Fact]
    public void INV_D1_erp_modules_must_not_import_subscriber_billing_entities()
    {
        var forbiddenBillingNamespaces = new[]
        {
            "ERP.Domain.Billing",
            "ERP.Domain.Subscriptions.Entities",
        };

        var erpOperationalNamespaces = new[]
        {
            "ERP.Domain.Modules.Sales",
            "ERP.Domain.Modules.Purchasing",
            "ERP.Domain.Modules.Inventory",
            "ERP.Domain.Modules.Accounting",
            "ERP.Domain.Modules.Cash",
        };

        var violators = DomainAssembly.GetTypes()
            .Where(t => erpOperationalNamespaces.Any(ns =>
                (t.Namespace ?? "").StartsWith(ns, StringComparison.Ordinal)))
            .Where(t =>
            {
                var fields = t.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                return fields.Any(f => forbiddenBillingNamespaces.Any(fn =>
                    (f.FieldType.Namespace ?? "").StartsWith(fn, StringComparison.Ordinal)));
            })
            .Select(t => t.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "Entidades COMPANY ERP no pueden referenciar entidades SUBSCRIBER billing. " +
            "Violación del COMPANY ↔ SUBSCRIBER boundary.");
    }

    // ── ENFORCEMENT LAYER INTEGRITY ───────────────────────────────────────────

    /// <summary>
    /// INV-E1: Los 3 interceptores del enforcement layer deben existir en Infrastructure.
    /// Garante que la capa de enforcement no fue eliminada accidentalmente.
    /// </summary>
    [Fact]
    public void INV_E1_all_three_enforcement_interceptors_must_exist()
    {
        var required = new[]
        {
            "ERP.Infrastructure.Persistence.Interceptors.CompanyTenantInterceptor",
            "ERP.Infrastructure.Persistence.Interceptors.DbCommandTenantInterceptor",
            "ERP.Infrastructure.Persistence.Interceptors.PostgreSqlSessionContextInterceptor",
        };

        foreach (var name in required)
        {
            InfraAssembly.GetType(name).Should().NotBeNull(
                $"Interceptor '{name}' eliminado del enforcement layer. " +
                "Este interceptor es obligatorio para el zero-leak guarantee.");
        }
    }

    /// <summary>
    /// INV-E2: Todo request MediatR en módulos COMPANY debe declarar scope explícito.
    /// Previene requests sin validación de tenant context.
    /// </summary>
    [Fact]
    public void INV_E2_company_module_requests_must_declare_explicit_scope()
    {
        var companyModulePrefixes = new[]
        {
            "ERP.Application.Modules.Sales",
            "ERP.Application.Modules.Purchasing",
            "ERP.Application.Modules.Inventory",
            "ERP.Application.Modules.Accounting",
            "ERP.Application.Modules.Cash",
            "ERP.Application.Sales",
        };

        var violators = AppAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => typeof(MediatR.IBaseRequest).IsAssignableFrom(t))
            .Where(t => companyModulePrefixes.Any(p =>
                (t.Namespace ?? "").StartsWith(p, StringComparison.Ordinal)))
            .Where(t =>
                !typeof(ERP.Application.Common.ICompanyScopedRequest).IsAssignableFrom(t) &&
                !typeof(ERP.Application.Common.ITenantScopedRequest).IsAssignableFrom(t) &&
                !typeof(ERP.Application.Common.IPlatformScopedRequest).IsAssignableFrom(t))
            .Select(t => t.FullName)
            .ToList();

        violators.Should().BeEmpty(
            "Requests en módulos COMPANY deben implementar ICompanyScopedRequest " +
            "(o ITenantScopedRequest / IPlatformScopedRequest). " +
            "Sin scope explícito el CompanyScopeBehavior no valida company context (fail-closed).");
    }
}

using ERP.Domain.Audit;
using ERP.Domain.Exceptions;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.MasterData.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.MasterData;

/// <summary>EXPENSE-SUPPLIER-SUBCATEGORIES-01 — invariantes y eventos auditables (ADR-022) de la asignación.</summary>
public sealed class SupplierExpenseSubcategoryTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid Supplier = Guid.NewGuid();
    private static readonly Guid Subcategory = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static SupplierExpenseSubcategory NewAssignment() =>
        SupplierExpenseSubcategory.Create(Tenant, Company, Supplier, Subcategory, User);

    [Fact]
    public void Create_deja_la_asignacion_activa_con_scope_y_levanta_Assigned()
    {
        var a = NewAssignment();

        a.IsActive.Should().BeTrue();
        a.TenantId.Should().Be(Tenant);
        a.CompanyId.Should().Be(Company);
        a.BusinessPartnerId.Should().Be(Supplier);
        a.ExpenseSubcategoryId.Should().Be(Subcategory);
        a.CreatedBy.Should().Be(User);
        var e = a.DomainEvents.Should().ContainSingle().Which.Should()
            .BeOfType<SupplierExpenseSubcategoryAssignedEvent>().Subject;
        ((IAuditEvent)e).Action.Should().Be("Assigned");
        ((IAuditEvent)e).EntityId.Should().Be(a.Id);
        e.CompanyId.Should().Be(Company);
    }

    [Fact]
    public void Disable_y_Enable_reutilizan_el_registro_y_levantan_su_evento()
    {
        var a = NewAssignment();
        var id = a.Id;
        a.ClearDomainEvents();

        a.Disable(User);
        a.IsActive.Should().BeFalse();
        a.UpdatedBy.Should().Be(User);
        ((IAuditEvent)a.DomainEvents.Single()).Action.Should().Be("Disabled");

        a.ClearDomainEvents();
        a.Enable(User);
        a.IsActive.Should().BeTrue();
        a.Id.Should().Be(id);
        ((IAuditEvent)a.DomainEvents.Single()).Action.Should().Be("Enabled");
    }

    [Fact]
    public void Disable_y_Enable_rechazan_el_estado_ya_aplicado()
    {
        var a = NewAssignment();

        a.Invoking(x => x.Enable(User)).Should().Throw<DomainRuleViolationException>();
        a.Disable(User);
        a.Invoking(x => x.Disable(User)).Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Create_exige_tenant_empresa_proveedor_y_subcategoria()
    {
        Action[] invalid =
        [
            () => SupplierExpenseSubcategory.Create(Guid.Empty, Company, Supplier, Subcategory, User),
            () => SupplierExpenseSubcategory.Create(Tenant, Guid.Empty, Supplier, Subcategory, User),
            () => SupplierExpenseSubcategory.Create(Tenant, Company, Guid.Empty, Subcategory, User),
            () => SupplierExpenseSubcategory.Create(Tenant, Company, Supplier, Guid.Empty, User),
        ];
        foreach (var act in invalid)
            act.Should().Throw<ArgumentException>();
    }
}

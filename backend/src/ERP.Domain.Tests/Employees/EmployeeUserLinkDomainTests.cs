using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Employees;

/// <summary>EMP-E6 — vínculo de usuario en la ficha (EMP-BR-13/14, D-E6-1/2).</summary>
public sealed class EmployeeUserLinkDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static Employee NewEmployee()
    {
        var e = Employee.Create(Tenant, Company, Guid.NewGuid(), new DateOnly(2026, 1, 10), User);
        e.ClearDomainEvents();
        return e;
    }

    [Fact]
    public void Vincular_y_desvincular_dejan_el_rastro_solo_con_identificadores()
    {
        var e = NewEmployee();
        var identity = Guid.NewGuid();

        e.LinkUser(identity, User);
        e.IdentityUserId.Should().Be(identity);
        e.UnlinkUser(User);
        e.IdentityUserId.Should().BeNull();

        e.DomainEvents.OfType<EmployeeUserLinkChangedEvent>()
            .Select(x => (x.Action, x.OldIdentityUserId, x.NewIdentityUserId))
            .Should().Equal(("UserLinked", null, identity), ("UserUnlinked", identity, null));
        (e.CompanyId, e.IsActive).Should().Be((Company, true), "no altera el estado laboral");
    }

    [Fact]
    public void Una_ficha_vinculada_no_se_reemplaza_ni_con_el_mismo_usuario()
    {
        var e = NewEmployee();
        var identity = Guid.NewGuid();
        e.LinkUser(identity, User);

        FluentActions.Invoking(() => e.LinkUser(identity, User)).Should().Throw<DomainRuleViolationException>();
        FluentActions.Invoking(() => e.LinkUser(Guid.NewGuid(), User)).Should().Throw<DomainRuleViolationException>();
        e.IdentityUserId.Should().Be(identity);
    }

    [Fact]
    public void Desvincular_sin_vinculo_y_un_identificador_vacio_se_rechazan()
    {
        var e = NewEmployee();
        FluentActions.Invoking(() => e.UnlinkUser(User)).Should().Throw<DomainRuleViolationException>();
        FluentActions.Invoking(() => e.LinkUser(Guid.Empty, User)).Should().Throw<ArgumentException>();
        e.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Una_ficha_en_baja_admite_vincular_y_desvincular()
    {
        var e = NewEmployee();
        e.Terminate(new DateOnly(2026, 6, 30), null, [], User);

        e.LinkUser(Guid.NewGuid(), User);
        e.UnlinkUser(User);

        e.IsActive.Should().BeFalse();
    }
}

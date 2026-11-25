using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Employees;

/// <summary>EMP-E5 — baja, reincorporación y asignaciones: invariantes de dominio (INV-06, 08, 09, 12, 13).</summary>
public sealed class EmployeeLifecycleDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static DateOnly D(int m, int d) => new(2026, m, d);

    private static Employee NewEmployee(DateOnly? start = null)
    {
        var e = Employee.Create(Tenant, Company, Guid.NewGuid(), start ?? D(1, 10), User);
        e.ClearDomainEvents();
        return e;
    }

    private static EmployeeBranchAssignment Assignment(Employee e, DateOnly start, DateOnly? end = null) =>
        EmployeeBranchAssignment.Create(Tenant, Company, e.Id, Guid.NewGuid(), start, end, User);

    [Fact]
    public void Baja_cierra_el_periodo_acorta_las_vigentes_conserva_las_terminadas_y_deja_la_ficha_inactiva()
    {
        var e = NewEmployee();
        var open = Assignment(e, D(1, 10));
        var longer = Assignment(e, D(2, 1), D(12, 31));
        var past = Assignment(e, D(1, 10), D(2, 28));

        e.Terminate(D(6, 30), "  Renuncia  ", [open, longer, past], User);

        e.IsActive.Should().BeFalse();
        e.OpenPeriod.Should().BeNull();
        e.Periods.Single().Should().BeEquivalentTo(new { EndDate = (DateOnly?)D(6, 30), TerminationReason = "Renuncia" });
        open.EndDate.Should().Be(D(6, 30));
        longer.EndDate.Should().Be(D(6, 30));
        past.EndDate.Should().Be(D(2, 28));
        e.DomainEvents.OfType<EmployeeTerminatedEvent>().Should().ContainSingle().Which.Reason.Should().Be("  Renuncia  ");
        e.DomainEvents.OfType<EmployeeBranchAssignmentChangedEvent>().Should().HaveCount(2);
    }

    [Fact]
    public void Baja_de_un_solo_dia_es_valida_y_las_invalidas_se_defienden()
    {
        var e = NewEmployee(D(3, 1));
        FluentActions.Invoking(() => e.Terminate(D(2, 28), null, [], User)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => e.Terminate(D(6, 30), new string('x', 501), [], User)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => e.Terminate(D(6, 30), null, [Assignment(e, D(7, 1))], User))
            .Should().Throw<DomainRuleViolationException>("asignación posterior a la baja");
        e.IsActive.Should().BeTrue("las defensas no dejan estado parcial");

        e.Terminate(D(3, 1), null, [], User);
        e.Periods.Single().EndDate.Should().Be(D(3, 1));
        FluentActions.Invoking(() => e.Terminate(D(3, 2), null, [], User)).Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Reincorporacion_abre_un_periodo_nuevo_estrictamente_posterior_sin_reabrir_nada()
    {
        var e = NewEmployee();
        var a = Assignment(e, D(1, 10));
        e.Terminate(D(6, 30), null, [a], User);
        e.ClearDomainEvents();

        FluentActions.Invoking(() => e.Rehire(D(6, 30), User)).Should().Throw<DomainRuleViolationException>("mismo día de la baja");
        FluentActions.Invoking(() => e.Rehire(default, User)).Should().Throw<DomainRuleViolationException>();

        var period = e.Rehire(D(7, 1), User);

        e.IsActive.Should().BeTrue();
        period.EndDate.Should().BeNull();
        e.Periods.Should().HaveCount(2);
        e.Periods.First(p => p != period).EndDate.Should().Be(D(6, 30), "no modifica períodos anteriores");
        a.EndDate.Should().Be(D(6, 30), "no reabre asignaciones");
        e.DomainEvents.OfType<EmployeeRehiredEvent>().Should().ContainSingle();
        FluentActions.Invoking(() => e.Rehire(D(8, 1), User)).Should().Throw<DomainRuleViolationException>("ya activa");
    }

    [Theory]
    [InlineData(1, 10, 1, 31, 1, 31, 2, 5, true)] // comparten el 31
    [InlineData(1, 10, 1, 30, 1, 31, 2, 5, false)] // adyacentes
    [InlineData(2, 1, 0, 0, 1, 1, 1, 31, false)] // abierta posterior
    [InlineData(1, 1, 0, 0, 3, 1, 3, 5, true)] // abierta cubre
    public void Solapamiento_inclusivo_con_fin_abierto(
        int sm, int sd, int em, int ed, int nsm, int nsd, int nem, int ned, bool overlaps)
    {
        var e = NewEmployee(D(1, 1));
        var existing = Assignment(e, D(sm, sd), em == 0 ? null : D(em, ed));
        existing.Overlaps(D(nsm, nsd), D(nem, ned)).Should().Be(overlaps);
    }

    [Fact]
    public void Cerrar_una_asignacion_ya_cerrada_o_con_fin_anterior_al_inicio_se_rechaza()
    {
        var e = NewEmployee();
        var a = Assignment(e, D(2, 1));
        FluentActions.Invoking(() => a.Close(D(1, 31), User)).Should().Throw<ArgumentException>();
        a.Close(D(2, 1), User); // un solo día es válido
        a.EndDate.Should().Be(D(2, 1));
        FluentActions.Invoking(() => a.Close(D(3, 1), User)).Should().Throw<DomainRuleViolationException>();
    }
}

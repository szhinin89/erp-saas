using ERP.Domain.Exceptions;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Events;
using ERP.Domain.Modules.Employees.Services;
using FluentAssertions;

namespace ERP.Domain.Tests.Employees;

/// <summary>EMP-E4 — edición de cargo, profesión y especialidades, y factoría de catálogos.</summary>
public sealed class EmployeeEditingDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static Employee NewEmployee()
    {
        var e = Employee.Create(Tenant, Company, Guid.NewGuid(), new DateOnly(2026, 3, 1), User);
        e.ClearDomainEvents();
        return e;
    }

    [Fact]
    public void ChangeJobPosition_y_ChangeProfession_actualizan_levantan_evento_y_conservan_la_empresa()
    {
        var e = NewEmployee();
        var job = Guid.NewGuid();
        var profession = Guid.NewGuid();

        e.ChangeJobPosition(job, User);
        e.ChangeProfession(profession, User);

        (e.JobPositionId, e.ProfessionId).Should().Be((job, profession));
        (e.CompanyId, e.TenantId).Should().Be((Company, Tenant));
        e.UpdatedBy.Should().Be(User);
        e.DomainEvents.OfType<EmployeeFieldChangedEvent>()
            .Select(x => (x.ChangedField, x.OldValue, x.NewValue))
            .Should().Equal(
                ("JobPosition", null, job.ToString()),
                ("Profession", null, profession.ToString()));
    }

    [Fact]
    public void Eliminar_la_referencia_con_null_es_un_cambio_auditable_y_repetir_el_valor_no_hace_nada()
    {
        var e = NewEmployee();
        var job = Guid.NewGuid();
        e.ChangeJobPosition(job, User);
        e.ClearDomainEvents();

        e.ChangeJobPosition(job, User);
        e.DomainEvents.Should().BeEmpty("sin cambio no hay evento");

        e.ChangeJobPosition(null, User);
        e.JobPositionId.Should().BeNull();
        e.DomainEvents.OfType<EmployeeFieldChangedEvent>().Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { OldValue = job.ToString(), NewValue = (string?)null });
    }

    [Fact]
    public void Una_referencia_Guid_Empty_se_rechaza()
    {
        var e = NewEmployee();
        FluentActions.Invoking(() => e.ChangeJobPosition(Guid.Empty, User)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => e.ChangeProfession(Guid.Empty, User)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecordSpecialtyChange_deja_rastro_de_asignacion_y_retiro()
    {
        var e = NewEmployee();
        var specialty = Guid.NewGuid();

        e.RecordSpecialtyChange(specialty, assigned: true, User);
        e.RecordSpecialtyChange(specialty, assigned: false, User);

        e.DomainEvents.OfType<EmployeeFieldChangedEvent>()
            .Select(x => (x.ChangedField, x.OldValue, x.NewValue))
            .Should().Equal(
                ("Specialty", null, specialty.ToString()),
                ("Specialty", specialty.ToString(), null));
    }

    [Fact]
    public void Touch_marca_la_ficha_como_modificada_sin_evento()
    {
        var e = NewEmployee();
        e.Touch(User);
        e.UpdatedAt.Should().NotBeNull();
        e.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void EmployeeSpecialty_alterna_is_active_en_la_misma_fila_de_forma_idempotente()
    {
        var link = EmployeeSpecialty.Create(Tenant, Company, Guid.NewGuid(), Guid.NewGuid(), User);
        var id = link.Id;

        link.Deactivate(User);
        link.Deactivate(User);
        link.IsActive.Should().BeFalse();
        link.Activate(User);
        link.Activate(User);

        link.IsActive.Should().BeTrue();
        link.Id.Should().Be(id);
    }

    [Theory]
    [InlineData(EmployeeCatalogType.JobPosition, typeof(JobPosition))]
    [InlineData(EmployeeCatalogType.Profession, typeof(Profession))]
    [InlineData(EmployeeCatalogType.Specialty, typeof(Specialty))]
    public void La_factoria_crea_el_catalogo_del_tipo_con_nombre_normalizado(EmployeeCatalogType type, Type expected)
    {
        var entry = EmployeeCatalogFactory.Create(type, Tenant, Company, "  Médico  General ", User);

        entry.Should().BeOfType(expected);
        entry.CatalogType.Should().Be(type);
        entry.Name.Should().Be("Médico General");
        entry.NormalizedName.Should().Be("medico general");
        entry.IsActive.Should().BeTrue();
        entry.CompanyId.Should().Be(Company);
    }

    [Fact]
    public void Activar_o_inactivar_dos_veces_es_una_violacion_de_regla_de_dominio()
    {
        var entry = EmployeeCatalogFactory.Create(EmployeeCatalogType.Specialty, Tenant, Company, "X", User);
        entry.Deactivate(User);
        FluentActions.Invoking(() => entry.Deactivate(User)).Should().Throw<DomainRuleViolationException>();
        entry.Activate(User);
        FluentActions.Invoking(() => entry.Activate(User)).Should().Throw<DomainRuleViolationException>();
    }
}

using ERP.Domain.Common;
using ERP.Domain.Exceptions;
using ERP.Domain.Kernel;
using ERP.Domain.Kernel.Permissions;
using ERP.Domain.Modules.Employees.Entities;
using ERP.Domain.Modules.Employees.Enums;
using ERP.Domain.Modules.Employees.Events;
using ERP.Domain.Modules.Employees.Services;
using FluentAssertions;

namespace ERP.Domain.Tests.Employees;

public sealed class EmployeesDomainTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Company = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    [Fact]
    public void Create_abre_el_primer_periodo_y_levanta_el_evento_de_auditoria()
    {
        var bp = Guid.NewGuid();
        var e = Employee.Create(Tenant, Company, bp, new DateOnly(2026, 3, 1), User);

        e.IsActive.Should().BeTrue();
        e.CompanyId.Should().Be(Company);
        e.JobPositionId.Should().BeNull();
        e.ProfessionId.Should().BeNull();
        e.IdentityUserId.Should().BeNull();
        var period = e.Periods.Should().ContainSingle().Subject;
        period.StartDate.Should().Be(new DateOnly(2026, 3, 1));
        period.EndDate.Should().BeNull();
        period.EmployeeId.Should().Be(e.Id);
        e.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<EmployeeCreatedEvent>()
            .Which.BusinessPartnerId.Should().Be(bp);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Create_rechaza_identificadores_vacios(bool badTenant, bool badCompany, bool badBp)
    {
        var act = () => Employee.Create(
            badTenant ? Guid.Empty : Tenant,
            badCompany ? Guid.Empty : Company,
            badBp ? Guid.Empty : Guid.NewGuid(),
            new DateOnly(2026, 1, 1),
            User);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_exige_fecha_de_ingreso()
    {
        var act = () => Employee.Create(Tenant, Company, Guid.NewGuid(), default, User);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CompanyId_no_tiene_setter_publico_en_ninguna_entidad_de_Employees()
    {
        var types = typeof(Employee).Assembly.GetTypes()
            .Where(t => t.Namespace == "ERP.Domain.Modules.Employees.Entities" && !t.IsAbstract
                && !t.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false))
            .ToList();
        types.Should().NotBeEmpty();
        foreach (var t in types)
        {
            var prop = t.GetProperty("CompanyId");
            prop.Should().NotBeNull(t.Name);
            (prop!.SetMethod?.IsPublic ?? false).Should().BeFalse(t.Name);
            typeof(ICompanyOperationalEntity).IsAssignableFrom(t).Should().BeTrue(t.Name);
        }
    }

    [Fact]
    public void Asignacion_exige_fechas_coherentes_y_acepta_un_solo_dia()
    {
        var e = Guid.NewGuid();
        var b = Guid.NewGuid();
        var day = new DateOnly(2026, 5, 10);

        EmployeeBranchAssignment.Create(Tenant, Company, e, b, day, day, User).EndDate.Should().Be(day);
        EmployeeBranchAssignment.Create(Tenant, Company, e, b, day, null, User).EndDate.Should().BeNull();

        var act = () => EmployeeBranchAssignment.Create(Tenant, Company, e, b, day, day.AddDays(-1), User);
        act.Should().Throw<ArgumentException>();
        var noBranch = () => EmployeeBranchAssignment.Create(Tenant, Company, e, Guid.Empty, day, null, User);
        noBranch.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EmployeeSpecialty_nace_activa_y_exige_ids()
    {
        EmployeeSpecialty.Create(Tenant, Company, Guid.NewGuid(), Guid.NewGuid(), User).IsActive.Should().BeTrue();
        var act = () => EmployeeSpecialty.Create(Tenant, Company, Guid.NewGuid(), Guid.Empty, User);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("  Médico   General ", "medico general")]
    [InlineData("MÉDICO GENERAL", "medico general")]
    [InlineData("Médico\tGeneral", "medico general")]
    [InlineData("Ñandú", "nandu")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Normalizador_recorta_colapsa_espacios_y_no_distingue_mayusculas_ni_tildes(string? input, string expected) =>
        EmployeeNameNormalizer.Normalize(input).Should().Be(expected);

    [Fact]
    public void Catalogo_normaliza_nombre_levanta_eventos_y_alterna_estado()
    {
        var c = JobPosition.Create(Tenant, Company, "  Recepcionista  Principal ", User);

        c.Name.Should().Be("Recepcionista Principal");
        c.NormalizedName.Should().Be("recepcionista principal");
        c.IsActive.Should().BeTrue();
        c.CatalogType.Should().Be(EmployeeCatalogType.JobPosition);

        c.Rename("Recepcionista Senior", User);
        c.Rename("Recepcionista Senior", User); // sin cambio: no levanta evento
        c.Deactivate(User);
        var again = () => c.Deactivate(User);
        again.Should().Throw<DomainRuleViolationException>();
        c.Activate(User);
        var activeAgain = () => c.Activate(User);
        activeAgain.Should().Throw<DomainRuleViolationException>();

        c.DomainEvents.OfType<EmployeeCatalogChangedEvent>().Select(x => x.Action)
            .Should().Equal("Created", "Renamed", "Deactivated", "Activated");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Catalogo_rechaza_nombre_vacio(string name)
    {
        var act = () => Profession.Create(Tenant, Company, name, User);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Catalogo_rechaza_nombre_de_mas_de_150_caracteres()
    {
        var act = () => Specialty.Create(Tenant, Company, new string('a', 151), User);
        act.Should().Throw<ArgumentException>();
        Specialty.Create(Tenant, Company, new string('a', 150), User).Name.Length.Should().Be(150);
    }

    [Fact]
    public void Los_tres_catalogos_declaran_su_tipo()
    {
        Profession.Create(Tenant, Company, "X", User).CatalogType.Should().Be(EmployeeCatalogType.Profession);
        Specialty.Create(Tenant, Company, "X", User).CatalogType.Should().Be(EmployeeCatalogType.Specialty);
    }

    [Fact]
    public void Los_ocho_permisos_aprobados_son_asignables_por_el_modulo_y_no_entran_en_perfiles()
    {
        var expected = new[]
        {
            "employees.view", "employees.create", "employees.update", "employees.disable",
            "employees.assignments.manage", "employees.link-user",
            "employees.catalogs.view", "employees.catalogs.manage",
        };

        typeof(EmployeesPermissions).GetFields()
            .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!)
            .Should().BeEquivalentTo(expected);
        KernelRegistry.Permissions.Should().Contain(expected);
        // EMP-E7: EmployeesModule hace asignables exactamente los ocho.
        KernelRegistry.Modules.Select(m => m.Code).Should().Contain("employees");
        KernelRegistry.AssignablePermissionKeys
            .Where(p => p.StartsWith("employees.", StringComparison.Ordinal))
            .Should().BeEquivalentTo(expected);
        new[] { InitialProfileBundles.Biller, InitialProfileBundles.Accountant }
            .SelectMany(b => b.Permissions).Should()
            .NotContain(p => p.StartsWith("employees.", StringComparison.Ordinal));
    }
}

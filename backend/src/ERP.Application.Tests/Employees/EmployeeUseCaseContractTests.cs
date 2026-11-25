using System.Text.Json;
using ERP.Application.Common;
using ERP.Application.Modules.Employees.DTOs;
using ERP.Application.Modules.Employees.UseCases;
using ERP.Domain.Modules.Employees.Enums;
using FluentAssertions;

namespace ERP.Application.Tests.Employees;

/// <summary>EMP-E4 — contrato de entrada: PATCH omitido vs null (D-3), validadores y request markers.</summary>
public sealed class EmployeeUseCaseContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private sealed record Body(uint RowVersion, OptionalField<Guid?> JobPositionId = default, OptionalField<Guid?> ProfessionId = default);

    [Fact]
    public void Propiedad_omitida_no_esta_fijada_y_null_explicito_si()
    {
        var id = Guid.NewGuid();

        var omitted = JsonSerializer.Deserialize<Body>("""{"rowVersion":7}""", Web)!;
        omitted.JobPositionId.IsSet.Should().BeFalse();
        omitted.ProfessionId.IsSet.Should().BeFalse();

        var explicitNull = JsonSerializer.Deserialize<Body>("""{"rowVersion":7,"jobPositionId":null}""", Web)!;
        explicitNull.JobPositionId.Should().Be(new OptionalField<Guid?>(true, null));
        explicitNull.ProfessionId.IsSet.Should().BeFalse();

        var value = JsonSerializer.Deserialize<Body>($$"""{"rowVersion":7,"professionId":"{{id}}"}""", Web)!;
        value.ProfessionId.Should().Be(new OptionalField<Guid?>(true, id));
        value.JobPositionId.IsSet.Should().BeFalse();
    }

    [Fact]
    public void Create_no_acepta_tenant_ni_empresa_del_cuerpo_y_es_company_scoped()
    {
        typeof(CreateEmployeeCommand).GetProperties().Select(p => p.Name)
            .Should().NotContain(["TenantId", "CompanyId"]);
        foreach (var type in new[]
        {
            typeof(CreateEmployeeCommand), typeof(UpdateEmployeeCommand), typeof(SetEmployeeSpecialtiesCommand),
            typeof(GetEmployeeByIdQuery), typeof(ListEmployeesQuery), typeof(ListEmployeeCatalogQuery),
            typeof(GetEmployeeCatalogEntryQuery), typeof(CreateEmployeeCatalogEntryCommand),
            typeof(RenameEmployeeCatalogEntryCommand), typeof(SetEmployeeCatalogEntryActiveCommand),
            typeof(LinkEmployeeUserCommand), typeof(UnlinkEmployeeUserCommand),
        })
        {
            typeof(ICompanyScopedRequest).IsAssignableFrom(type).Should().BeTrue(type.Name);
            type.GetProperties().Select(p => p.Name).Should().NotContain(["TenantId", "CompanyId"], type.Name);
        }
    }

    [Fact]
    public void Validadores_rechazan_ids_vacios_nombres_invalidos_y_paginacion_fuera_de_rango()
    {
        new CreateEmployeeCommandValidator().Validate(
            new CreateEmployeeCommand(Guid.NewGuid(), new DateOnly(2026, 1, 1), null, null, [Guid.NewGuid()]))
            .IsValid.Should().BeTrue();
        new CreateEmployeeCommandValidator().Validate(
            new CreateEmployeeCommand(Guid.Empty, new DateOnly(2026, 1, 1), Guid.Empty, Guid.Empty, [Guid.Empty]))
            .Errors.Select(e => e.PropertyName).Should().Contain(["BusinessPartnerId", "JobPositionId", "ProfessionId"]);

        new UpdateEmployeeCommandValidator().Validate(new UpdateEmployeeCommand(Guid.NewGuid(), 1, OptionalField<Guid?>.Of(null)))
            .IsValid.Should().BeTrue("null explícito es válido (D-3)");
        new UpdateEmployeeCommandValidator().Validate(new UpdateEmployeeCommand(Guid.NewGuid(), 1, OptionalField<Guid?>.Of(Guid.Empty)))
            .IsValid.Should().BeFalse();

        new SetEmployeeSpecialtiesCommandValidator().Validate(new SetEmployeeSpecialtiesCommand(Guid.NewGuid(), 1, [Guid.Empty]))
            .IsValid.Should().BeFalse();

        var type = EmployeeCatalogType.JobPosition;
        new CreateEmployeeCatalogEntryCommandValidator().Validate(new CreateEmployeeCatalogEntryCommand(type, "Cajero")).IsValid.Should().BeTrue();
        new CreateEmployeeCatalogEntryCommandValidator().Validate(new CreateEmployeeCatalogEntryCommand(type, "  ")).IsValid.Should().BeFalse();
        new CreateEmployeeCatalogEntryCommandValidator().Validate(new CreateEmployeeCatalogEntryCommand(type, new string('x', 151))).IsValid.Should().BeFalse();
        new RenameEmployeeCatalogEntryCommandValidator().Validate(new RenameEmployeeCatalogEntryCommand(type, Guid.Empty, 1, "x")).IsValid.Should().BeFalse();
        new CreateEmployeeCatalogEntryCommandValidator().Validate(new CreateEmployeeCatalogEntryCommand((EmployeeCatalogType)99, "x")).IsValid.Should().BeFalse();

        new ListEmployeesQueryValidator().Validate(new ListEmployeesQuery()).IsValid.Should().BeTrue();
        new ListEmployeesQueryValidator().Validate(new ListEmployeesQuery(Skip: -1)).IsValid.Should().BeFalse();
        new ListEmployeesQueryValidator().Validate(new ListEmployeesQuery(Take: 0)).IsValid.Should().BeFalse();
        new ListEmployeesQueryValidator().Validate(new ListEmployeesQuery(Take: 201)).IsValid.Should().BeFalse();
        new ListEmployeeCatalogQueryValidator().Validate(new ListEmployeeCatalogQuery(type, Take: 500)).IsValid.Should().BeFalse();
    }
}

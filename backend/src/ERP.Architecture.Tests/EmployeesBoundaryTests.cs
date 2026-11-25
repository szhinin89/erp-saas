using System.Text.RegularExpressions;
using FluentAssertions;

namespace ERP.Architecture.Tests;

/// <summary>
/// EMP-E6b — límites del módulo Empleados: otros módulos consumen <c>IEmployeeDirectory</c> y nunca sus
/// entidades, repositorios, eventos, configuraciones ni tablas. Las únicas excepciones son los archivos de
/// composición de la persistencia, enumerados uno a uno (sin comodines de módulo).
/// </summary>
public sealed class EmployeesBoundaryTests
{
    private static readonly Regex Internal = new(
        @"ERP\.Domain\.Modules\.Employees\.(Entities|Interfaces|Events|Services)\b"
            + @"|ERP\.Infrastructure\.Persistence\.(Repositories|Configurations)\.Employees\b"
            + @"|ERP\.Application\.Modules\.Employees\.(EventHandlers)\b"
            + @"|\b_?(?:db|context)\.(?:Employees|Employee[A-Za-z]+)\b"
            + @"|\bSet<Employee[A-Za-z]*>\(",
        RegexOptions.Compiled
    );

    /// <summary>Carpetas propias del módulo.</summary>
    private static readonly string[] OwnedFolders =
    [
        "ERP.Domain/Modules/Employees/",
        "ERP.Application/Modules/Employees/",
        "ERP.Infrastructure/Persistence/Repositories/Employees/",
        "ERP.Infrastructure/Persistence/Configurations/Employees/",
    ];

    /// <summary>Composición de la persistencia: registro de dependencias, DbSets y baseline generada.</summary>
    private static readonly string[] CompositionFiles =
    [
        "ERP.Infrastructure/DependencyInjection.cs",
        "ERP.Infrastructure/Persistence/ErpDbContext.cs",
        "ERP.Infrastructure/Migrations/20261101012214_InitialProductionBaseline.cs",
        "ERP.Infrastructure/Migrations/20261101012214_InitialProductionBaseline.Designer.cs",
        "ERP.Infrastructure/Migrations/ErpDbContextModelSnapshot.cs",
    ];

    private static readonly string[] ProductionProjects =
        ["ERP.Domain", "ERP.Application", "ERP.Infrastructure", "ERP.API"];

    private static string SourceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "ERP.Domain")))
            dir = dir.Parent;
        dir.Should().NotBeNull("no se encontró backend/src");
        return dir!.FullName;
    }

    private static IEnumerable<(string Relative, string Text)> Sources(params string[] projects)
    {
        var root = SourceRoot();
        foreach (var project in projects)
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Contains("/bin/") || relative.Contains("/obj/"))
                continue;
            yield return (relative, File.ReadAllText(file));
        }
    }

    [Fact]
    public void Ningun_otro_modulo_accede_a_los_internos_ni_a_las_tablas_de_Empleados()
    {
        var violations = Sources(ProductionProjects)
            .Where(s => !OwnedFolders.Any(f => s.Relative.StartsWith(f, StringComparison.Ordinal)))
            .Where(s => !CompositionFiles.Contains(s.Relative))
            .SelectMany(s => Internal.Matches(s.Text).Select(m => $"{s.Relative}: {m.Value}"))
            .ToList();

        violations.Should().BeEmpty("los consumidores usan IEmployeeDirectory y sus modelos");
    }

    [Theory]
    [InlineData("using ERP.Domain.Modules.Employees.Entities;")]
    [InlineData("ERP.Domain.Modules.Employees.Interfaces.IEmployeeRepository repo;")]
    [InlineData("using ERP.Infrastructure.Persistence.Repositories.Employees;")]
    [InlineData("var x = await _context.Employees.ToListAsync();")]
    [InlineData("var x = db.EmployeeBranchAssignments.Where(a => true);")]
    [InlineData("var x = context.Set<Employee>();")]
    public void La_regla_detecta_un_acceso_directo(string code) => Internal.IsMatch(code).Should().BeTrue();

    [Theory]
    [InlineData("using ERP.Application.Modules.Employees.Interfaces;")]
    [InlineData("using ERP.Application.Modules.Employees.DTOs;")]
    [InlineData("using ERP.Domain.Modules.Employees.Enums;")]
    [InlineData("private readonly IEmployeeDirectory _employees;")]
    public void La_regla_permite_el_contrato_publico(string code) => Internal.IsMatch(code).Should().BeFalse();

    [Fact]
    public void El_directorio_esta_en_Application_se_implementa_en_Infrastructure_y_se_registra_scoped()
    {
        var contract = typeof(ERP.Application.Modules.Employees.Interfaces.IEmployeeDirectory);
        contract.Assembly.GetName().Name.Should().Be("ERP.Application");
        contract.GetMethods().Select(m => m.Name).Should().BeEquivalentTo(
            "GetByIdAsync", "ListAsync", "IsActiveOnAsync", "GetBranchAssignmentsAsync",
            "IsEligibleForBranchAsync", "GetLinkedUserAsync");

        var implementations = Sources(ProductionProjects)
            .Where(s => Regex.IsMatch(s.Text, @"class\s+\w+\s*:\s*[^{]*\bIEmployeeDirectory\b"))
            .Select(s => s.Relative)
            .ToList();
        implementations.Should().Equal("ERP.Infrastructure/Persistence/Repositories/Employees/EmployeeDirectory.cs");

        var registration = Sources("ERP.Infrastructure").Single(s => s.Relative == "ERP.Infrastructure/DependencyInjection.cs").Text;
        registration.Should().MatchRegex(@"AddScoped<[\w.]*IEmployeeDirectory,\s*[\w.]*EmployeeDirectory>\(\)");
    }

    [Fact]
    public void Los_modelos_del_directorio_no_exponen_tipos_de_dominio_y_Application_no_depende_de_Infrastructure()
    {
        var models = typeof(ERP.Application.Modules.Employees.DTOs.EmployeeSummary).Assembly
            .GetTypes()
            .Where(t => t.Name is "EmployeeSummary" or "EmployeeAssignmentSummary" or "EmployeePeriodSummary" or "EmployeeDirectoryFilter")
            .ToList();
        models.Should().HaveCount(4);
        foreach (var property in models.SelectMany(m => m.GetProperties()))
        {
            var types = property.PropertyType.IsGenericType
                ? property.PropertyType.GetGenericArguments().Append(property.PropertyType)
                : [property.PropertyType];
            types.Should().NotContain(t => t.Namespace != null && t.Namespace.StartsWith("ERP.Domain"), property.Name);
        }

        typeof(ERP.Application.Modules.Employees.Interfaces.IEmployeeDirectory).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .Should().NotContain(["ERP.Infrastructure", "ERP.API"]);
    }
}

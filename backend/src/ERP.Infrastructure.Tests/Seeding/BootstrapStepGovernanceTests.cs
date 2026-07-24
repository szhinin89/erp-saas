using System.Reflection;
using System.Text.RegularExpressions;
using ERP.Application.Common.Interfaces;
using ERP.Infrastructure;
using ERP.Infrastructure.Seeding.Global;
using FluentAssertions;

namespace ERP.Infrastructure.Tests.Seeding;

/// <summary>
/// Gates de arquitectura del bootstrap (empresa y global): órdenes únicos, todo step registrado
/// en DI (y viceversa), y ausencia de acoplamiento directo entre steps. Un nuevo BootstrapStep que
/// incumpla cualquiera de estas reglas hace fallar esta suite — no requiere tocarla para agregar
/// un step nuevo correctamente implementado.
/// </summary>
public sealed class BootstrapStepGovernanceTests
{
    [Fact]
    public void Company_bootstrap_step_orders_are_unique()
    {
        ConstantValues(typeof(CompanyBootstrapStepOrder)).Should().OnlyHaveUniqueItems(
            "dos steps con el mismo Order producen un orden de ejecución ambiguo");
    }

    [Fact]
    public void Global_bootstrap_step_orders_are_unique()
    {
        ConstantValues(typeof(GlobalBootstrapStepOrder)).Should().OnlyHaveUniqueItems(
            "dos steps con el mismo Order producen un orden de ejecución ambiguo");
    }

    [Fact]
    public void Every_ICompanyBootstrapStep_implementation_is_registered_in_DI_and_vice_versa()
    {
        var implementors = DiscoverImplementors(typeof(ICompanyBootstrapStep));
        var registered   = RegisteredTypeNames("ICompanyBootstrapStep");

        registered.Should().BeEquivalentTo(implementors,
            "todo ICompanyBootstrapStep debe registrarse en DependencyInjection.cs, y todo lo " +
            "registrado ahí debe seguir existiendo como clase — evita steps huérfanos u olvidados");
    }

    [Fact]
    public void Every_IGlobalBootstrapStep_implementation_is_registered_in_DI_and_vice_versa()
    {
        var implementors = DiscoverImplementors(typeof(IGlobalBootstrapStep));
        var registered   = RegisteredTypeNames("IGlobalBootstrapStep");

        registered.Should().BeEquivalentTo(implementors,
            "todo IGlobalBootstrapStep debe registrarse en DependencyInjection.cs, y todo lo " +
            "registrado ahí debe seguir existiendo como clase — evita steps huérfanos u olvidados");
    }

    [Fact]
    public void No_bootstrap_step_depends_directly_on_another_bootstrap_step()
    {
        var allStepTypes = DiscoverImplementorTypes(typeof(ICompanyBootstrapStep))
            .Concat(DiscoverImplementorTypes(typeof(IGlobalBootstrapStep)))
            .ToHashSet();

        var violations = new List<string>();

        foreach (var stepType in allStepTypes)
        {
            var ctor = stepType.GetConstructors().Single();
            foreach (var param in ctor.GetParameters())
            {
                if (allStepTypes.Contains(param.ParameterType))
                    violations.Add($"{stepType.Name} depende directamente de {param.ParameterType.Name}");
            }
        }

        violations.Should().BeEmpty(
            "un step nunca debe conocer ni depender de otro step — la comunicación entre steps " +
            "ocurre exclusivamente vía estado ya persistido en base de datos, consultado por " +
            "quien lo necesite, nunca por referencia directa");
    }

    private static List<int> ConstantValues(Type constantsClass) =>
        constantsClass
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(int))
            .Select(f => (int)f.GetRawConstantValue()!)
            .ToList();

    // Las interfaces viven en ERP.Application; sus implementaciones (los steps) viven en
    // ERP.Infrastructure — por eso se escanea el ensamblado de Infrastructure, no el de la interfaz.
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjection).Assembly;

    private static HashSet<string> DiscoverImplementors(Type stepInterface) =>
        DiscoverImplementorTypes(stepInterface).Select(t => t.Name).ToHashSet();

    private static IEnumerable<Type> DiscoverImplementorTypes(Type stepInterface) =>
        InfrastructureAssembly.GetTypes()
            .Where(t => stepInterface.IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false });

    private static HashSet<string> RegisteredTypeNames(string interfaceSimpleName)
    {
        var diFile = Path.Combine(ResolveBackendRoot(), "src", "ERP.Infrastructure", "DependencyInjection.cs");
        var text = File.ReadAllText(diFile);

        // Captura la última clase calificada antes de '>' en: AddScoped<...InterfaceSimpleName, [ns.]ClassName>
        var pattern = $@"{interfaceSimpleName}\s*,\s*(?:[\w.]+\.)?(\w+)\s*>";
        return Regex.Matches(text, pattern)
            .Select(m => m.Groups[1].Value)
            .ToHashSet();
    }

    private static string ResolveBackendRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ERP.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, "src", "ERP.API")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("No se encontró la raíz backend (ERP.API / ERP.sln).");
    }
}

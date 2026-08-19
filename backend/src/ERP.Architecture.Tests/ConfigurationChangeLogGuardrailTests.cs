using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// CONFIG-FOUNDATION-P2-01: confirma que el único punto de escritura a org_settings
/// (OrgSettingsRepository) depende de IConfigurationChangeLogger — si alguien reintroduce un
/// segundo camino de escritura a org_settings que no pase por este guardrail, este test no lo
/// detecta directamente (eso es responsabilidad de OrgSettingsAccessGuardrailTests), pero si
/// OrgSettingsRepository pierde la dependencia del logger, este test sí falla.
/// </summary>
public sealed class ConfigurationChangeLogGuardrailTests
{
    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void OrgSettingsRepository_depends_on_IConfigurationChangeLogger()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .HaveNameEndingWith("OrgSettingsRepository")
            .Should()
            .HaveDependencyOn("ERP.Domain.Configuration.Interfaces.IConfigurationChangeLogger")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(
                "OrgSettingsRepository.UpsertAsync debe registrar cambios auditables via IConfigurationChangeLogger — "
                    + string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())
            );
    }

    [Fact]
    public void ConfigurationChangeLogger_is_the_only_implementation_of_IConfigurationChangeLogger()
    {
        var implementations = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ImplementInterface(typeof(ERP.Domain.Configuration.Interfaces.IConfigurationChangeLogger))
            .GetTypes()
            .Select(t => t.FullName)
            .ToList();

        implementations
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be("ERP.Infrastructure.Services.ConfigurationChangeLogger");
    }
}

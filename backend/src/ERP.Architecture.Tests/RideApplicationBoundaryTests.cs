using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §2/§8: los contratos de Ride en Application solo pueden depender de Domain (y de
/// terceros de propósito general como MediatR/FluentValidation) — nunca de Infrastructure, API,
/// QuestPDF, System.Xml, Sales o ElectronicDocuments. Fase 3 del plan de implementación de Ride
/// solo entrega Application, así que este gate cubre esa capa además de la de Domain ya cubierta
/// por <see cref="RideModuleBoundaryTests"/>.
/// </summary>
public sealed class RideApplicationBoundaryTests
{
    private const string RideApplicationNamespace = "ERP.Application.Modules.Ride";

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    [Fact]
    public void Ride_application_must_not_reference_infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(RideApplicationNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_application_must_not_reference_api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(RideApplicationNamespace)
            .ShouldNot().HaveDependencyOn("ERP.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_application_must_not_reference_questpdf()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(RideApplicationNamespace)
            .ShouldNot().HaveDependencyOn("QuestPDF")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    /// <summary>
    /// Corrección de alcance (Fase 6): la restricción original de Fase 3 cubría todo el namespace
    /// de Ride, pero <c>InvoiceXmlBuilder</c> (ElectronicDocuments, FROZEN) ya establece el
    /// precedente real del repo — el parseo de XML usa <c>System.Xml.Linq</c> directamente en
    /// Application, no se empuja a Infrastructure. Lo que ADR-025 §6 realmente exige es que
    /// <c>RideModel</c>, una vez parseado, "nunca dependa del XML de nuevo" — no que ningún
    /// código del módulo toque XML, lo cual sería imposible para el propio parser. El gate ahora
    /// excluye <c>Parsers/</c> (única carpeta cuyo trabajo es leer XML) y cubre el resto.
    /// </summary>
    [Fact]
    public void Ride_application_must_not_reference_system_xml_outside_the_parsers_folder()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(RideApplicationNamespace)
            .And().DoNotResideInNamespace("ERP.Application.Modules.Ride.Parsers")
            .ShouldNot().HaveDependencyOn("System.Xml")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_application_must_not_reference_sales()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(RideApplicationNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_application_must_not_reference_electronic_documents()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace(RideApplicationNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

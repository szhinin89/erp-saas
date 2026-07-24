using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §13 (Fase 7 del plan de implementación): <c>QuestPdfRideRenderer</c> es una
/// transformación pura <c>IRideDocumentLayout → byte[]</c> — nunca vuelve a interpretar XML, ni
/// conoce ElectronicDocuments, Sales o el parser.
/// </summary>
public sealed class RideQuestPdfRendererBoundaryTests
{
    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;

    private const string RenderingNamespace = "ERP.Infrastructure.Ride.Rendering";

    [Fact]
    public void Renderer_and_sections_do_not_depend_on_system_xml()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RenderingNamespace)
            .ShouldNot().HaveDependencyOn("System.Xml")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Renderer_and_sections_do_not_depend_on_electronic_documents()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RenderingNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Renderer_and_sections_do_not_depend_on_sales()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RenderingNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Renderer_and_sections_do_not_depend_on_the_parser()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RenderingNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.Ride.Parsers")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void QuestPdfRideRenderer_public_render_method_takes_only_IRideDocumentLayout()
    {
        var rendererType = typeof(ERP.Infrastructure.Ride.Rendering.QuestPdfRideRenderer);
        var renderMethod = rendererType.GetMethod("RenderAsync");

        renderMethod.Should().NotBeNull();
        var parameters = renderMethod!.GetParameters();
        parameters[0].ParameterType.Name.Should().Be(nameof(ERP.Application.Modules.Ride.Rendering.IRideDocumentLayout));
    }
}

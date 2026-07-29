using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §6/§9 (Fase 5 del plan de implementación): <c>RidePipeline</c> orquesta exclusivamente
/// mediante interfaces — nunca conoce clases concretas de Infrastructure, ni Sales,
/// ElectronicDocuments o QuestPDF. Este gate cubre la carpeta <c>Modules/Ride/Services</c>
/// (RidePipeline, RideDocumentService, RideCacheStrategy, RideContentHasher).
/// </summary>
public sealed class RidePipelineBoundaryTests
{
    private const string RideServicesNamespace = "ERP.Application.Modules.Ride.Services";

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    [Fact]
    public void Ride_pipeline_must_not_reference_infrastructure()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(RideServicesNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_pipeline_must_not_reference_sales()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(RideServicesNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_pipeline_must_not_reference_electronic_documents()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(RideServicesNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_pipeline_must_not_reference_questpdf()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(RideServicesNamespace)
            .ShouldNot()
            .HaveDependencyOn("QuestPDF")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_pipeline_depends_only_on_abstractions_within_ride_application()
    {
        // RidePipeline no debe referenciar ninguna clase concreta de Infrastructure.Ride
        // (Rendering/Branding/Qr/Storage) — solo sus interfaces, ya cubiertas por el gate anterior.
        // Este test documenta explícitamente la intención de la Fase 5: Strategy resuelto por DI,
        // nunca por new() directo de una implementación.
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("RidePipeline")
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure.Ride")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

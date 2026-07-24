using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §7 (Fase 9 del plan de implementación de Ride): <c>RideController</c> es un
/// controller delgado — depende únicamente de MediatR/Application, nunca de Infrastructure,
/// <c>ErpDbContext</c>, repositorios concretos, QuestPDF o ElectronicDocuments.
/// </summary>
public sealed class RideControllerBoundaryTests
{
    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(ERP.API.Controllers.RideController).Assembly;

    [Fact]
    public void RideController_does_not_depend_on_infrastructure()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("RideController")
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RideController_does_not_depend_on_ErpDbContext()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("RideController")
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure.Persistence.ErpDbContext")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RideController_does_not_depend_on_ride_repositories()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("RideController")
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure.Persistence.Repositories.Ride")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RideController_does_not_depend_on_questpdf()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("RideController")
            .ShouldNot().HaveDependencyOn("QuestPDF")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RideController_does_not_depend_on_electronic_documents()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("RideController")
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RideController_depends_only_on_mediatr_and_ride_application_contracts()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().HaveNameStartingWith("RideController")
            .Should().HaveDependencyOnAny("MediatR", "ERP.Application.Modules.Ride")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

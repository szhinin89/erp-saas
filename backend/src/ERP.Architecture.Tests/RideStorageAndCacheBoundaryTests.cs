using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §14/§15 (Fase 8 del plan de implementación de Ride): storage, cache y branding
/// definitivos mantienen los mismos límites de capa que el resto del módulo.
/// </summary>
public sealed class RideStorageAndCacheBoundaryTests
{
    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    private const string StorageNamespace = "ERP.Infrastructure.Ride.Storage";
    private const string CacheServicesNamespace = "ERP.Application.Modules.Ride.Services";
    private const string BrandingNamespace = "ERP.Infrastructure.Ride.Branding";

    [Fact]
    public void Storage_does_not_depend_on_questpdf()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(StorageNamespace)
            .ShouldNot()
            .HaveDependencyOn("QuestPDF")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Storage_does_not_depend_on_sales()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(StorageNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Storage_does_not_depend_on_electronic_documents()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(StorageNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Cache_does_not_depend_on_infrastructure_rendering()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CacheServicesNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure.Ride.Rendering")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Branding_does_not_depend_on_the_parser()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(BrandingNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Ride.Parsers")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

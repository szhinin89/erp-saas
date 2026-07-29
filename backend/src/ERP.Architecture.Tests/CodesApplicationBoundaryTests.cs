using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// Codes es un Building Block transversal de generación de códigos (QR hoy; Code128, Data
/// Matrix, PDF417, Aztec en el futuro). Sus contratos en Application deben permanecer
/// completamente genéricos: ninguna dependencia hacia Infrastructure, API, QuestPDF ni ningún
/// módulo de negocio (Ride, ElectronicDocuments, Sales) — cualquier dominio del ERP debe poder
/// depender de <c>IQrCodeGenerator</c> sin arrastrar conocimiento de otro módulo.
/// </summary>
public sealed class CodesApplicationBoundaryTests
{
    private const string CodesApplicationNamespace = "ERP.Application.Codes";

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    [Fact]
    public void Codes_application_must_not_reference_infrastructure()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CodesApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_application_must_not_reference_api()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CodesApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.API")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_application_must_not_reference_questpdf()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CodesApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn("QuestPDF")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_application_must_not_reference_ride()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CodesApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Ride")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_application_must_not_reference_electronic_documents()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CodesApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_application_must_not_reference_sales()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .ResideInNamespace(CodesApplicationNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// La implementación concreta del Building Block Codes (hoy: <c>QrCodeGenerator</c> sobre
/// QRCoder) debe permanecer reutilizable por cualquier módulo del ERP — nunca acoplada a Ride,
/// ElectronicDocuments, Sales ni a la API.
/// </summary>
public sealed class CodesInfrastructureBoundaryTests
{
    private const string CodesInfrastructureNamespace = "ERP.Infrastructure.Codes";

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Codes_infrastructure_must_not_reference_api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(CodesInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("ERP.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_infrastructure_must_not_reference_ride()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(CodesInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure.Ride")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_infrastructure_must_not_reference_electronic_documents()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(CodesInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure.ElectronicDocuments")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_infrastructure_must_not_reference_sales()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(CodesInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Codes_infrastructure_must_not_reference_questpdf()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(CodesInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("QuestPDF")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

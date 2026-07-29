using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §2/§5: Ride es un módulo independiente — su Domain no puede conocer Application,
/// Infrastructure, API, Sales ni ElectronicDocuments. Fase 2 del plan de implementación de Ride
/// solo entrega Domain, así que por ahora este gate cubre exclusivamente esa capa.
/// </summary>
public sealed class RideModuleBoundaryTests
{
    private const string RideDomainNamespace = "ERP.Domain.Modules.Ride";

    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(ERP.Domain.Common.BaseEntity).Assembly;

    [Fact]
    public void Ride_domain_must_not_reference_application()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace(RideDomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_domain_must_not_reference_infrastructure()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace(RideDomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_domain_must_not_reference_api()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace(RideDomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.API")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_domain_must_not_reference_sales()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace(RideDomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Domain.Modules.Sales")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_domain_must_not_reference_electronic_documents()
    {
        var result = Types
            .InAssembly(DomainAssembly)
            .That()
            .ResideInNamespace(RideDomainNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Domain.Modules.ElectronicDocuments")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

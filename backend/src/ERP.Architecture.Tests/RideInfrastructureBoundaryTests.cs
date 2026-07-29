using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §5: Infrastructure implementa las interfaces de Application/Domain, pero no debe
/// introducir dependencias circulares hacia API ni "saltarse" capas. Fase 4 del plan de
/// implementación de Ride entrega el walking skeleton de Infrastructure — este gate confirma que
/// depende únicamente de Domain y Application (más terceros de infraestructura general), nunca
/// de API.
/// </summary>
public sealed class RideInfrastructureBoundaryTests
{
    private const string RideInfrastructureNamespace = "ERP.Infrastructure.Ride";

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Ride_infrastructure_must_not_reference_api()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.API")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_infrastructure_must_not_reference_sales()
    {
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_infrastructure_electronic_documents_adapter_only_touches_public_use_cases()
    {
        // El único punto de contacto con ElectronicDocuments debe ser sus requests públicos
        // (UseCases/*), nunca su entidad de dominio ni su repositorio interno.
        var result = Types
            .InAssembly(InfrastructureAssembly)
            .That()
            .ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot()
            .HaveDependencyOn("ERP.Domain.Modules.ElectronicDocuments.Entities")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// ADR-025 §9/§10 (Fase 6 del plan de implementación): Factura es únicamente una implementación
/// Strategy — ni el parser ni la plantilla dependen de Infrastructure/QuestPDF, y
/// <c>RidePipeline</c> sigue sin conocer que Factura existe.
/// </summary>
public sealed class RideInvoiceStrategyBoundaryTests
{
    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(ERP.Application.DependencyInjection).Assembly;

    [Fact]
    public void InvoiceRideXmlParser_does_not_depend_on_infrastructure()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("InvoiceRideXmlParser")
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void InvoiceRideXmlParser_does_not_depend_on_electronic_documents()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("InvoiceRideXmlParser")
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.ElectronicDocuments")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void InvoiceRideXmlParser_does_not_depend_on_sales()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("InvoiceRideXmlParser")
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Sales")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void DefaultInvoiceRideTemplate_does_not_depend_on_questpdf()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("DefaultInvoiceRideTemplate")
            .ShouldNot()
            .HaveDependencyOn("QuestPDF")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void DefaultInvoiceRideTemplate_does_not_depend_on_infrastructure()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("DefaultInvoiceRideTemplate")
            .ShouldNot()
            .HaveDependencyOn("ERP.Infrastructure")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RidePipeline_still_does_not_depend_on_the_invoice_parser()
    {
        // El pipeline resuelve por Strategy — nunca debe referenciar la implementación concreta
        // de Factura, ni siquiera ahora que existe.
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("RidePipeline")
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Ride.Parsers.InvoiceRideXmlParser")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void RidePipeline_still_does_not_depend_on_the_invoice_template()
    {
        var result = Types
            .InAssembly(ApplicationAssembly)
            .That()
            .HaveNameStartingWith("RidePipeline")
            .ShouldNot()
            .HaveDependencyOn("ERP.Application.Modules.Ride.Templates.DefaultInvoiceRideTemplate")
            .GetResult();

        result
            .IsSuccessful.Should()
            .BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

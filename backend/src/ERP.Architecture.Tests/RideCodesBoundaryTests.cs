using FluentAssertions;
using NetArchTest.Rules;

namespace ERP.Architecture.Tests;

/// <summary>
/// Fase 11 (hardening del Building Block Codes): garantiza automáticamente que Ride solo puede
/// depender de las abstracciones <c>IRideQrCodeGenerator</c>/<c>IRideBarcodeGenerator</c> —
/// nunca de QRCoder/ZXing/SkiaSharp directamente (lo que también cubre <c>new
/// QRCodeGenerator(...)</c>/<c>new Code128BarcodeGenerator(...)</c>, que exigen referenciar el
/// tipo) ni de la implementación concreta de Codes (<c>ERP.Infrastructure.Codes.*</c>). El lado
/// "Codes nunca depende de Ride/Sales/ElectronicDocuments/QuestPDF" ya está cubierto por
/// <see cref="CodesApplicationBoundaryTests"/>/<see cref="CodesInfrastructureBoundaryTests"/>
/// (Fase 10) — no se duplica aquí.
/// </summary>
public sealed class RideCodesBoundaryTests
{
    private const string RideInfrastructureNamespace = "ERP.Infrastructure.Ride";

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(ERP.Infrastructure.DependencyInjection).Assembly;

    [Fact]
    public void Ride_must_not_depend_on_qrcoder()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("QRCoder")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_must_not_depend_on_zxing()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("ZXing")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_must_not_depend_on_skiasharp()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("SkiaSharp")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void Ride_must_not_depend_on_the_concrete_codes_implementation()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .That().ResideInNamespace(RideInfrastructureNamespace)
            .ShouldNot().HaveDependencyOn("ERP.Infrastructure.Codes")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }
}

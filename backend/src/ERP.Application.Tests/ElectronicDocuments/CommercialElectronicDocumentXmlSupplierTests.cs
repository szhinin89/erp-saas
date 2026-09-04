using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Application.Modules.ElectronicDocuments.Services;
using ERP.Application.Modules.ElectronicDocuments.XmlBuilders;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicDocuments;

/// <summary>
/// RETENTIONS-SRI-AUTHORIZATION-WIRING-04D — <see cref="CommercialElectronicDocumentXmlSupplier"/>:
/// reproduce exactamente las dos llamadas (provider→builder) que
/// <see cref="ElectronicDocumentIssuer"/> hacía directamente antes de esta fase.
/// </summary>
public sealed class CommercialElectronicDocumentXmlSupplierTests
{
    private static readonly ElectronicDocumentSourceReference Reference = new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid()
    );

    private static ElectronicDocumentData SampleData(ElectronicDocumentType documentType) =>
        new(
            Emission: new ElectronicDocumentEmissionContext(
                "1",
                "1",
                documentType == ElectronicDocumentType.Invoice ? "01" : "04",
                "001",
                "Dirección",
                "001",
                "000000001",
                DateTime.UtcNow
            ),
            Issuer: new ElectronicDocumentIssuerData(
                "1792146739001",
                "Empresa Prueba",
                null,
                "Matriz",
                null,
                false
            ),
            Counterparty: new ElectronicDocumentCounterpartyData(
                "05",
                "1713328506",
                "Cliente Prueba",
                null,
                null
            ),
            Details: [],
            TaxSummary: [],
            Totals: new ElectronicDocumentTotals(0, 0, 0, 0, "USD"),
            Payments: [],
            AdditionalInfo: []
        );

    private static ElectronicDocumentXml SampleXml(ElectronicDocumentType documentType) =>
        new(
            Xml: "<factura/>",
            Encoding: "UTF-8",
            Version: "1.1.0",
            DocumentType: documentType,
            Environment: "1",
            AccessKey: new string('1', 49),
            GeneratedAtUtc: DateTime.UtcNow
        );

    [Fact]
    public async Task BuildXmlAsync_for_invoice_uses_the_current_provider_and_builder()
    {
        var data = SampleData(ElectronicDocumentType.Invoice);
        var xml = SampleXml(ElectronicDocumentType.Invoice);

        var provider = new Mock<IElectronicDocumentDataProvider>();
        provider
            .Setup(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentData>.Success(data));

        var builder = new Mock<IElectronicDocumentXmlBuilder>();
        builder.Setup(b => b.Build(data)).Returns(Result<ElectronicDocumentXml>.Success(xml));

        var supplier = new CommercialElectronicDocumentXmlSupplier(
            ElectronicDocumentType.Invoice,
            provider.Object,
            builder.Object
        );

        var result = await supplier.BuildXmlAsync(Reference, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(xml);
        supplier.DocumentType.Should().Be(ElectronicDocumentType.Invoice);
        provider.Verify(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()), Times.Once);
        builder.Verify(b => b.Build(data), Times.Once);
    }

    [Fact]
    public async Task BuildXmlAsync_for_creditnote_uses_the_current_provider_and_builder()
    {
        var data = SampleData(ElectronicDocumentType.CreditNote);
        var xml = SampleXml(ElectronicDocumentType.CreditNote);

        var provider = new Mock<IElectronicDocumentDataProvider>();
        provider
            .Setup(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentData>.Success(data));

        var builder = new Mock<IElectronicDocumentXmlBuilder>();
        builder.Setup(b => b.Build(data)).Returns(Result<ElectronicDocumentXml>.Success(xml));

        var supplier = new CommercialElectronicDocumentXmlSupplier(
            ElectronicDocumentType.CreditNote,
            provider.Object,
            builder.Object
        );

        var result = await supplier.BuildXmlAsync(Reference, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(xml);
        supplier.DocumentType.Should().Be(ElectronicDocumentType.CreditNote);
    }

    [Fact]
    public async Task BuildXmlAsync_propagates_a_provider_failure_without_calling_the_builder()
    {
        var provider = new Mock<IElectronicDocumentDataProvider>();
        provider
            .Setup(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentData>.NotFound("El documento de origen no existe."));

        var builder = new Mock<IElectronicDocumentXmlBuilder>();

        var supplier = new CommercialElectronicDocumentXmlSupplier(
            ElectronicDocumentType.Invoice,
            provider.Object,
            builder.Object
        );

        var result = await supplier.BuildXmlAsync(Reference, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("El documento de origen no existe.");
        builder.Verify(b => b.Build(It.IsAny<ElectronicDocumentData>()), Times.Never);
    }

    [Fact]
    public async Task BuildXmlAsync_propagates_a_builder_failure()
    {
        var data = SampleData(ElectronicDocumentType.Invoice);
        var provider = new Mock<IElectronicDocumentDataProvider>();
        provider
            .Setup(p => p.GetDataAsync(Reference, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ElectronicDocumentData>.Success(data));

        var builder = new Mock<IElectronicDocumentXmlBuilder>();
        builder
            .Setup(b => b.Build(data))
            .Returns(Result<ElectronicDocumentXml>.ValidationFailure("El detalle es obligatorio."));

        var supplier = new CommercialElectronicDocumentXmlSupplier(
            ElectronicDocumentType.Invoice,
            provider.Object,
            builder.Object
        );

        var result = await supplier.BuildXmlAsync(Reference, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("El detalle es obligatorio.");
    }
}
